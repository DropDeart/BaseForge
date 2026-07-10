using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>
/// <see cref="AuthSpec"/>'ten config-driven bir merkez auth (Identity) servisi üretir.
/// Kod dosyaları gömülü referans servisten alınır (namespace değiştirilir), yapılandırma
/// (<c>appsettings.Auth</c>) spec'ten üretilir.
/// </summary>
internal static class IdentityGenerator
{
    private const string ResourcePrefix = "identity/";
    private const string WebResourcePrefix = "identity-web/";
    private const string ReferenceNamespace = "BaseForge.Identity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyList<string> Generate(AuthSpec spec, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var ns = NameUtil.Pascal(spec.Service);
        var written = new List<string>();

        // 1) Kod dosyaları: gömülü referanstan namespace değiştirilerek.
        var assembly = typeof(IdentityGenerator).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            var code = reader.ReadToEnd().Replace(ReferenceNamespace, ns, StringComparison.Ordinal);

            var relative = resource[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            written.Add(WriteFile(Path.Combine(outputDir, relative), code));
        }

        // 1b) Ortak Giriş SPA'sı (Login/Register/Admin): önceden derlenmiş dist çıktısı, wwwroot altına
        // ham bayt olarak kopyalanır (font/JS gibi binary dosyalar için metin okuma/değiştirme uygulanmaz).
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(WebResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            var relative = resource[WebResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var path = Path.Combine(outputDir, "wwwroot", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = File.Create(path);
            stream.CopyTo(file);
            written.Add(path);
        }

        // 2) Yapılandırma ve altyapı dosyaları.
        written.Add(WriteFile(Path.Combine(outputDir, ns + ".csproj"), BuildProject(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, "appsettings.json"), BuildAppSettings(spec)));
        written.Add(WriteFile(Path.Combine(outputDir, ".env.example"), BuildEnvExample(spec)));
        written.Add(WriteFile(Path.Combine(outputDir, ".env"), BuildEnvReal(spec)));
        written.Add(WriteFile(Path.Combine(outputDir, "Properties", "launchSettings.json"), BuildLaunchSettings(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, "Dockerfile"), BuildDockerfile(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, ".dockerignore"), "bin/\nobj/\n**/bin/\n**/obj/\n.vs/\n.git/\n*.user\n"));
        written.Add(WriteFile(Path.Combine(outputDir, ".gitignore"), "bin/\nobj/\n**/bin/\n**/obj/\n.vs/\n*.user\n\n# Gerçek secret'lar — commit edilmez (bkz. .env.example)\n.env\n"));
        written.Add(WriteFile(Path.Combine(outputDir, "docker-compose.yml"), BuildCompose(spec)));

        // Workspace kökündeki paylaşılan servis kaydına kendini ekle, sonra güncel halini kendi
        // wwwroot'una kopyala — dashboard'un "Servisler" bölümü bunu statik dosya olarak okuyacak.
        ServiceRegistry.UpsertIdentity(outputDir, spec);
        ServiceRegistry.SnapshotForIdentity(outputDir);

        return written;
    }

    private static string BuildProject(string ns) =>
        $$"""
        <Project Sdk="Microsoft.NET.Sdk.Web">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{ns}}</RootNamespace>
            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
            <GenerateDocumentationFile>false</GenerateDocumentationFile>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.9" />
            <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
            <PackageReference Include="OpenIddict.AspNetCore" Version="7.5.0" />
            <PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.5.0" />
            <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.9" />
            <PackageReference Include="Microsoft.AspNetCore.Authentication.MicrosoftAccount" Version="10.0.9" />
            <PackageReference Include="Microsoft.AspNetCore.Authentication.Facebook" Version="10.0.9" />
            <PackageReference Include="AspNet.Security.OAuth.GitHub" Version="10.0.0" />
            <!-- Merkez kullanıcı (User) entity'sine diğer servislerin gRPC ile salt-okunur erişimi -->
            <PackageReference Include="Grpc.AspNetCore" Version="2.71.0" />
            <!-- Yerel 'dotnet run' için .env dosyasını okuyup process environment'a yükler -->
            <PackageReference Include="DotNetEnv" Version="3.1.1" />
          </ItemGroup>

          <ItemGroup>
            <Protobuf Include="Protos/user.proto" GrpcServices="Server" />
          </ItemGroup>

        </Project>

        """;

    private static string BuildAppSettings(AuthSpec spec)
    {
        var postgresPort = spec.DockerPorts?.Postgres ?? 5432;
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Default"] = $"Host=localhost;Port={postgresPort};Database={spec.Database};Username=baseforge;Password=change_me",
            },
            ["Kestrel"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Endpoints"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Http"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["Url"] = "http://+:8080",
                        ["Protocols"] = "Http1",
                    },
                    ["Grpc"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["Url"] = "http://+:8081",
                        ["Protocols"] = "Http2",
                    },
                },
            },
            ["Auth"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Issuer"] = string.IsNullOrWhiteSpace(spec.Issuer) ? "http://localhost:5090/" : spec.Issuer,
                ["SigningCertificatePath"] = spec.Signing?.CertificatePath ?? string.Empty,
                ["SigningCertificatePassword"] = spec.Signing?.CertificatePassword ?? string.Empty,
                ["Scopes"] = spec.Scopes.Select(s => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Name"] = s.Name,
                    ["Resource"] = s.Resource,
                }).ToList(),
                // NOT: Secret/Password alanları burada bilerek boş bırakılır — gerçek değerler
                // sadece '.env' dosyasında tutulur (Auth__Clients__{i}__Secret / Auth__SeedAdmin__Password),
                // appsettings.json git'e commit edilebilir kalsın diye.
                ["Clients"] = spec.Clients.Select(c => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ClientId"] = c.ClientId,
                    ["Secret"] = c.Public ? null : string.Empty,
                    ["Public"] = c.Public,
                    ["Grants"] = c.Grants,
                    ["Scopes"] = c.Scopes,
                    ["RedirectUris"] = c.RedirectUris,
                }).ToList(),
                ["SeedAdmin"] = spec.SeedAdmin is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Email"] = spec.SeedAdmin.Email,
                    ["Password"] = string.Empty,
                },
                ["Providers"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Google"] = Provider(spec.Providers.Google),
                    ["GitHub"] = Provider(spec.Providers.GitHub),
                    ["Microsoft"] = Provider(spec.Providers.Microsoft),
                    ["Facebook"] = Provider(spec.Providers.Facebook),
                },
            },
            ["Logging"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["LogLevel"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Default"] = "Information",
                    ["Microsoft.AspNetCore"] = "Warning",
                },
            },
            ["AllowedHosts"] = "*",
        };

        return JsonSerializer.Serialize(settings, JsonOptions) + Environment.NewLine;
    }

    private static Dictionary<string, object?> Provider(ProviderSpec? provider) =>
        new(StringComparer.Ordinal)
        {
            ["ClientId"] = provider?.ClientId ?? string.Empty,
            // ClientSecret bilerek boş — gerçek değer '.env'de (Auth__Providers__{Name}__ClientSecret).
            ["ClientSecret"] = string.Empty,
        };

    private static string BuildEnvExample(AuthSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Format referansı — gerçek değerler otomatik '.env'e yazıldı (bu dosya git'e commit edilebilir).");
        sb.AppendLine("# docker-compose 'env_file' ile, yerel 'dotnet run' DotNetEnv ile bu formatı okur.");
        sb.AppendLine();
        if (spec.SeedAdmin is not null)
        {
            sb.AppendLine("Auth__SeedAdmin__Password=");
        }

        for (var i = 0; i < spec.Clients.Count; i++)
        {
            if (!spec.Clients[i].Public)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"# {spec.Clients[i].ClientId}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Clients__{i}__Secret=");
            }
        }

        sb.AppendLine();
        sb.AppendLine("# Dış sağlayıcı secret'ları (kullanılacaksa):");
        foreach (var name in new[] { "Google", "GitHub", "Microsoft", "Facebook" })
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Providers__{name}__ClientSecret=");
        }

        return sb.ToString();
    }

    /// <summary>
    /// <see cref="BuildEnvExample"/> ile aynı anahtarlar, ama boş olanlar atlanır ve doluysa gerçek
    /// değer yazılır. Bu dosya (<c>.env</c>) commit edilmez — bkz. üretilen <c>.gitignore</c>.
    /// </summary>
    private static string BuildEnvReal(AuthSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# GERÇEK secret'lar — commit ETMEYİN. Format referansı için .env.example'a bakın.");
        sb.AppendLine();

        if (spec.SeedAdmin is not null && !string.IsNullOrWhiteSpace(spec.SeedAdmin.Password))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__SeedAdmin__Password={spec.SeedAdmin.Password}");
        }

        for (var i = 0; i < spec.Clients.Count; i++)
        {
            if (!spec.Clients[i].Public && !string.IsNullOrWhiteSpace(spec.Clients[i].Secret))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"# {spec.Clients[i].ClientId}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Clients__{i}__Secret={spec.Clients[i].Secret}");
            }
        }

        var providers = new (string Name, ProviderSpec? Spec)[]
        {
            ("Google", spec.Providers.Google),
            ("GitHub", spec.Providers.GitHub),
            ("Microsoft", spec.Providers.Microsoft),
            ("Facebook", spec.Providers.Facebook),
        };

        foreach (var (name, provider) in providers)
        {
            if (provider is not null && !string.IsNullOrWhiteSpace(provider.ClientSecret))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Providers__{name}__ClientSecret={provider.ClientSecret}");
            }
        }

        return sb.ToString();
    }

    private static string BuildLaunchSettings(string ns) =>
        $$"""
        {
          "$schema": "https://json.schemastore.org/launchsettings.json",
          "profiles": {
            "{{ns}}": {
              "commandName": "Project",
              "launchBrowser": false,
              "applicationUrl": "http://localhost:5090",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }
          }
        }

        """;

    private static string BuildDockerfile(string ns) =>
        $"""
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet publish {ns}.csproj -c Release -o /app

        FROM mcr.microsoft.com/dotnet/aspnet:10.0
        WORKDIR /app
        COPY --from=build /app .
        ENTRYPOINT ["dotnet", "{ns}.dll"]

        """;

    private static string BuildCompose(AuthSpec spec)
    {
        var restPort = spec.DockerPorts?.Rest ?? 8081;
        var grpcPort = spec.DockerPorts?.Grpc ?? 8082;
        var postgresPort = spec.DockerPorts?.Postgres ?? 5432;
        // Docker registry kuralı: imaj adları (dolayısıyla compose servis anahtarı) büyük harf içeremez.
        var serviceKey = spec.Service.ToLowerInvariant();

        return $$"""
        # {{spec.Service}} merkez auth — izole test (servis + kendi PostgreSQL'i).
        #   docker compose up --build -d   ·   discovery: http://localhost:{{restPort}}/.well-known/openid-configuration
        services:
          postgres:
            image: postgres:17-alpine
            environment:
              POSTGRES_USER: baseforge
              POSTGRES_PASSWORD: change_me
              POSTGRES_DB: {{spec.Database}}
            healthcheck:
              test: ["CMD-SHELL", "pg_isready -U baseforge -d {{spec.Database}}"]
              interval: 10s
              timeout: 5s
              retries: 5
            ports:
              - "{{postgresPort}}:5432"   # yerelde 'dotnet run' + sadece bu postgres'i container'da çalıştırmak için (appsettings.json ile eşleşir)
            volumes:
              - {{spec.Service}}-pgdata:/var/lib/postgresql/data

          {{serviceKey}}:
            build: .
            env_file:
              - .env   # gerçek secret'lar (Auth__Providers__*__ClientSecret, Auth__SeedAdmin__Password, ...)
            environment:
              ASPNETCORE_ENVIRONMENT: Development
              Auth__Issuer: "http://{{serviceKey}}:8080/"
              ConnectionStrings__Default: "Host=postgres;Port=5432;Database={{spec.Database}};Username=baseforge;Password=change_me"
            ports:
              - "{{restPort}}:8080"   # REST/OpenIddict (HTTP/1.1)
              - "{{grpcPort}}:8081"   # gRPC (h2c) — merkez User entity'sine erişim
            volumes:
              - {{spec.Service}}-avatars:/app/wwwroot/uploads   # profil fotoğrafları — container recreate'te kaybolmasın
            depends_on:
              postgres:
                condition: service_healthy

        volumes:
          {{spec.Service}}-pgdata:
          {{spec.Service}}-avatars:

        """;
    }

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
