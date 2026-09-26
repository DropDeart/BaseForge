using BaseForge.CodeGen.Contracts;
using BaseForge.CodeGen.Generation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace BaseForge.CodeGen.Designer;

/// <summary>Designer arayüzünün tükettiği <c>/api/*</c> uçları.</summary>
internal static class DesignerEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Dropdown'lar için sabit meta bilgisi.
        api.MapGet("/meta", (DesignerContext ctx) =>
        {
            var solutionPath = SolutionRunner.FindNearestSolution(ctx.WorkingDirectory);
            var serviceSpecPath = Path.Combine(ResolveOutput(null, ctx, ctx.ServiceName), "spec.yaml");
            var identityAuthPath = Path.Combine(ResolveOutput(null, ctx, ResolveIdentityFolderName(ctx)), "auth.yaml");
            return Results.Ok(new MetaResponse(
                Types: TypeMap.KnownTypes,
                RelationKinds: ["one-to-many", "many-to-one", "one-to-one"],
                Via: ["grpc", "event"],
                Providers: ["Google", "GitHub", "Microsoft", "Facebook"],
                SolutionFound: solutionPath is not null,
                SolutionName: solutionPath is not null ? Path.GetFileName(solutionPath) : null,
                ServiceIsNew: !File.Exists(serviceSpecPath),
                IdentityIsNew: !File.Exists(identityAuthPath)));
        });

        // Workspace'te daha önce üretilmiş servislerin (identity dahil) kaydı — Designer'ın port/authority
        // önerisi üretmesi için (bkz. ServiceRegistry).
        api.MapGet("/workspace", (DesignerContext ctx) =>
            Results.Ok(ServiceRegistry.LoadForWorkspace(ctx.WorkingDirectory)));

        // 'new' -> CLI arg'ından seed edilmiş boş spec. 'update' -> diskteki spec.yaml/auth.yaml (varsa) yüklenir.
        api.MapGet("/spec", (DesignerContext ctx) => Results.Ok(new SpecResponse(
            Service: LoadServiceOrSeed(ctx),
            Auth: LoadAuthOrSeed(ctx))));

        // Servis spec doğrulama.
        api.MapPost("/validate", (ServiceSpec spec) =>
            Results.Ok(new ValidateResponse(SpecValidator.Validate(spec))));

        // Canlı ER diyagramı (draw.io XML).
        api.MapPost("/er", (ServiceSpec spec) =>
            Results.Text(DrawioErGenerator.Generate(spec), "application/xml"));

        // Servis üret: spec.yaml yaz + kod üret + dotnet build.
        api.MapPost("/generate/service", async (GenerateServiceRequest req, DesignerContext ctx, CancellationToken ct) =>
        {
            var spec = req.Spec;
            var errors = SpecValidator.Validate(spec);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new ValidateResponse(errors));
            }

            var output = ResolveOutput(req.Output, ctx, spec.Service);
            var specPath = YamlSpecWriter.Write(spec, output);
            var files = CodeGenerator.Generate(spec, output, specPath);
            var build = await BuildRunner.BuildAsync(output, ct);
            var solutionMessage = await TryAddToSolutionAsync(req.IncludeInSolution, ctx, files, ct);

            return Results.Ok(new GenerateResponse(output, files, build.Success, build.Output, solutionMessage));
        });

        // Merkez Identity üret: auth.yaml yaz + kod üret + dotnet build.
        api.MapPost("/generate/identity", async (GenerateIdentityRequest req, DesignerContext ctx, CancellationToken ct) =>
        {
            var spec = req.Spec;
            var errors = AuthSpecValidator.Validate(spec);
            if (errors.Count > 0)
            {
                return Results.BadRequest(new ValidateResponse(errors));
            }

            var output = ResolveOutput(null, ctx, spec.Service);
            // Secret'lar yalnızca .env'de tutulur: boş bırakılanları mevcut .env'den geri al, auth.yaml'a secret'sız yaz.
            IdentitySecrets.RestoreUnchanged(spec, output);
            YamlSpecWriter.Write(IdentitySecrets.Redacted(spec), output, "auth.yaml");
            var files = IdentityGenerator.Generate(spec, output);
            var build = await BuildRunner.BuildAsync(output, ct);
            var solutionMessage = await TryAddToSolutionAsync(req.IncludeInSolution, ctx, files, ct);

            return Results.Ok(new GenerateResponse(output, files, build.Success, build.Output, solutionMessage));
        });

        // Üretilen servisi çalıştır: docker compose up --build -d --wait (tam stack).
        api.MapPost("/run", async (RunRequest req, CancellationToken ct) =>
        {
            var result = await RunRunner.StartAsync(req.Output, ct);
            return Results.Ok(new RunResponse(result.Success, $"http://localhost:{req.RestPort}", result.DockerOutput));
        });

        // Çalışan servisi durdur.
        api.MapPost("/stop", async (StopRequest req, CancellationToken ct) =>
            Results.Ok(new StopResponse(await RunRunner.StopAsync(req.Output, ct))));

        // UI "Kapat" butonu.
        // Yanıt hemen döner (UI "kapatıldı" ekranına geçebilsin); başlatılan container'lar ve süreç yanıttan sonra kapanır.
        api.MapPost("/shutdown", (HttpContext http, IHostApplicationLifetime lifetime) =>
        {
            http.Response.OnCompleted(() => DesignerHeartbeat.ShutdownAsync(lifetime));
            return Results.Ok();
        });

        // Açık sekmenin nabzı: sekme kapatılırsa (Kapat'a basılmadan) sunucu bir süre sonra kendini kapatır.
        api.MapPost("/heartbeat", (DesignerHeartbeat heartbeat) =>
        {
            heartbeat.Touch();
            return Results.Ok();
        });

        // "Arayüz oluştur": ayrı bir tool olan 'uidesign'ı (BaseForge.UiDesigner.Cli) seçilen
        // servislerle başlatır. Tool kurulu değilse kurulum talimatı döner (bkz. UiDesignRunner).
        api.MapPost("/ui-design/launch", async (UiDesignLaunchRequest req, DesignerContext ctx, CancellationToken ct) =>
        {
            if (req.Services.Count == 0)
            {
                return Results.BadRequest(new UiDesignLaunchResponse(false, null, "En az bir servis seçilmelidir."));
            }

            var result = await UiDesignRunner.LaunchAsync(ctx.WorkingDirectory, req.Services, ct);
            return Results.Ok(new UiDesignLaunchResponse(result.Success, result.Url, result.Message));
        });
    }

    private static string ResolveOutput(string? requested, DesignerContext ctx, string service)
        => !string.IsNullOrWhiteSpace(requested)
            ? Path.GetFullPath(requested)
            : Path.GetFullPath(Path.Combine(ctx.WorkingDirectory, service));

    /// <summary>
    /// Workspace'te daha önce üretilmiş bir identity servisi varsa onun GERÇEK klasör/servis adını
    /// (<c>auth.yaml</c>'daki <c>Service</c>'i "identity" dışında bir şeye değiştirilmiş olsa bile)
    /// paylaşılan kayıttan (<see cref="ServiceRegistry"/>) döner; hiç üretilmemişse yeni bir identity
    /// için varsayılan öneri olan <c>"identity"</c>'ye düşer.
    /// </summary>
    private static string ResolveIdentityFolderName(DesignerContext ctx)
        => ServiceRegistry.LoadForWorkspace(ctx.WorkingDirectory).FirstOrDefault(e => e.IsIdentity)?.Name ?? "identity";

    /// <summary>
    /// Kullanıcı Designer'da "Solution'a ekle" seçtiyse, üretilen projeyi en yakın <c>.slnx</c>/<c>.sln</c>'e
    /// <c>/services/</c> çözüm klasörü altında ekler. Seçmediyse hiçbir şeye dokunulmaz — servis diskte
    /// bağımsız bir klasör olarak kalır (örn. ayrı bir repo/pipeline'dan yayınlanacaksa).
    /// </summary>
    private static async Task<string?> TryAddToSolutionAsync(
        bool includeInSolution, DesignerContext ctx, IReadOnlyList<string> files, CancellationToken ct)
    {
        if (!includeInSolution)
        {
            return null;
        }

        var solutionPath = SolutionRunner.FindNearestSolution(ctx.WorkingDirectory);
        if (solutionPath is null)
        {
            return "Yakında bir .slnx/.sln bulunamadı — servis solution'a eklenmedi.";
        }

        var csproj = files.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj is null)
        {
            return "Üretilen dosyalar arasında .csproj bulunamadı — solution'a eklenemedi.";
        }

        var result = await SolutionRunner.AddProjectAsync(solutionPath, csproj, "services", ct);
        return result.Success
            ? $"{Path.GetFileName(solutionPath)}'e eklendi (/services/)."
            : $"Solution'a eklenemedi: {result.Output}";
    }

    /// <summary>'update' ile açıldıysa ve <c>&lt;servis&gt;/spec.yaml</c> varsa onu yükler; aksi halde boş seed.</summary>
    private static ServiceSpec LoadServiceOrSeed(DesignerContext ctx)
    {
        if (ctx.LoadExisting)
        {
            var path = Path.Combine(ResolveOutput(null, ctx, ctx.ServiceName), "spec.yaml");
            if (File.Exists(path))
            {
                try
                {
                    return SpecLoader.Load(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Uyarı: '{path}' okunamadı ({ex.Message}) — boş spec ile açılıyor.");
                }
            }
        }

        return new ServiceSpec { Service = ctx.ServiceName, Database = $"{ctx.ServiceName}_db" };
    }

    /// <summary>'update' ile açıldıysa ve <c>identity/auth.yaml</c> varsa onu yükler; aksi halde boş seed.
    /// Gerçek secret'lar (ClientSecret, SeedAdmin.Password) tarayıcıya ASLA gönderilmez — Network sekmesinden
    /// bile okunabilir olmasınlar diye burada boşaltılır. Formda "..." placeholder'ı gösterilir; kullanıcı
    /// dokunmazsa <see cref="IdentitySecrets.RestoreUnchanged"/> kaydederken .env'deki gerçek değeri geri koyar.</summary>
    private static AuthSpec LoadAuthOrSeed(DesignerContext ctx)
    {
        if (ctx.LoadExisting)
        {
            var path = Path.Combine(ResolveOutput(null, ctx, ResolveIdentityFolderName(ctx)), "auth.yaml");
            if (File.Exists(path))
            {
                try
                {
                    var loaded = SpecLoader.Load<AuthSpec>(path);
                    MaskSecrets(loaded);
                    return loaded;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Uyarı: '{path}' okunamadı ({ex.Message}) — boş auth spec ile açılıyor.");
                }
            }
        }

        return new AuthSpec { Service = "identity", Database = "identity_db" };
    }

    private static void MaskSecrets(AuthSpec spec)
    {
        if (spec.SeedAdmin is not null)
        {
            spec.SeedAdmin.Password = string.Empty;
        }

        foreach (var client in spec.Clients)
        {
            if (!string.IsNullOrEmpty(client.Secret))
            {
                client.Secret = string.Empty;
            }
        }

        foreach (var provider in new[] { spec.Providers.Google, spec.Providers.GitHub, spec.Providers.Microsoft, spec.Providers.Facebook })
        {
            if (provider is not null)
            {
                provider.ClientSecret = string.Empty;
            }
        }
    }

    private sealed record MetaResponse(
        IReadOnlyList<string> Types,
        IReadOnlyList<string> RelationKinds,
        IReadOnlyList<string> Via,
        IReadOnlyList<string> Providers,
        bool SolutionFound,
        string? SolutionName,
        bool ServiceIsNew,
        bool IdentityIsNew);

    private sealed record SpecResponse(ServiceSpec Service, AuthSpec Auth);

    private sealed record ValidateResponse(IReadOnlyList<string> Errors);

    private sealed record GenerateServiceRequest(ServiceSpec Spec, string? Output, bool IncludeInSolution);

    private sealed record GenerateIdentityRequest(AuthSpec Spec, bool IncludeInSolution);

    private sealed record GenerateResponse(
        string Output,
        IReadOnlyList<string> Files,
        bool BuildSuccess,
        string BuildOutput,
        string? SolutionMessage);

    private sealed record RunRequest(string Output, int RestPort);

    private sealed record RunResponse(bool Success, string Url, string DockerOutput);

    private sealed record StopRequest(string Output);

    private sealed record StopResponse(bool Stopped);

    private sealed record UiDesignLaunchRequest(IReadOnlyList<string> Services);

    private sealed record UiDesignLaunchResponse(bool Success, string? Url, string Message);
}
