using BaseForge.CodeGen.Generation;
using BaseForge.CodeGen.Spec;
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
        api.MapGet("/meta", () => Results.Ok(new MetaResponse(
            Types: TypeMap.KnownTypes,
            RelationKinds: ["one-to-many", "many-to-one", "one-to-one"],
            Via: ["grpc", "event"],
            Providers: ["Google", "GitHub", "Microsoft", "Facebook"])));

        // CLI arg'ından seed edilmiş boş spec.
        api.MapGet("/spec", (DesignerContext ctx) => Results.Ok(new SpecResponse(
            Service: new ServiceSpec { Service = ctx.ServiceName, Database = $"{ctx.ServiceName}_db" },
            Auth: new AuthSpec { Service = "identity", Database = "identity_db" })));

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

            return Results.Ok(new GenerateResponse(output, files, build.Success, build.Output));
        });

        // Merkez Identity üret: auth.yaml yaz + kod üret + dotnet build.
        api.MapPost("/generate/identity", async (AuthSpec spec, DesignerContext ctx, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(spec.Service) || string.IsNullOrWhiteSpace(spec.Database))
            {
                return Results.BadRequest(new ValidateResponse(["'service' ve 'database' zorunludur."]));
            }

            var output = ResolveOutput(null, ctx, spec.Service);
            YamlSpecWriter.Write(spec, output, "auth.yaml");
            var files = IdentityGenerator.Generate(spec, output);
            var build = await BuildRunner.BuildAsync(output, ct);

            return Results.Ok(new GenerateResponse(output, files, build.Success, build.Output));
        });

        // UI "Kapat" butonu.
        api.MapPost("/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.Ok();
        });
    }

    private static string ResolveOutput(string? requested, DesignerContext ctx, string service)
        => !string.IsNullOrWhiteSpace(requested)
            ? Path.GetFullPath(requested)
            : Path.GetFullPath(Path.Combine(ctx.WorkingDirectory, service));

    private sealed record MetaResponse(
        IReadOnlyList<string> Types,
        IReadOnlyList<string> RelationKinds,
        IReadOnlyList<string> Via,
        IReadOnlyList<string> Providers);

    private sealed record SpecResponse(ServiceSpec Service, AuthSpec Auth);

    private sealed record ValidateResponse(IReadOnlyList<string> Errors);

    private sealed record GenerateServiceRequest(ServiceSpec Spec, string? Output);

    private sealed record GenerateResponse(
        string Output,
        IReadOnlyList<string> Files,
        bool BuildSuccess,
        string BuildOutput);
}
