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

        // 2) Yapılandırma ve altyapı dosyaları.
        written.Add(WriteFile(Path.Combine(outputDir, ns + ".csproj"), BuildProject(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, "appsettings.json"), BuildAppSettings(spec)));
        written.Add(WriteFile(Path.Combine(outputDir, ".env.example"), BuildEnvExample(spec)));
        written.Add(WriteFile(Path.Combine(outputDir, "Properties", "launchSettings.json"), BuildLaunchSettings(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, "Dockerfile"), BuildDockerfile(ns)));
        written.Add(WriteFile(Path.Combine(outputDir, ".dockerignore"), "bin/\nobj/\n**/bin/\n**/obj/\n.vs/\n.git/\n*.user\n"));
        written.Add(WriteFile(Path.Combine(outputDir, "docker-compose.yml"), BuildCompose(spec)));

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
          </ItemGroup>

          <ItemGroup>
            <Protobuf Include="Protos/user.proto" GrpcServices="Server" />
          </ItemGroup>

        </Project>

        """;

    private static string BuildAppSettings(AuthSpec spec)
    {
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Default"] = $"Host=localhost;Port=5432;Database={spec.Database};Username=baseforge;Password=change_me",
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
                ["Clients"] = spec.Clients.Select(c => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ClientId"] = c.ClientId,
                    ["Secret"] = c.Secret,
                    ["Public"] = c.Public,
                    ["Grants"] = c.Grants,
                    ["Scopes"] = c.Scopes,
                    ["RedirectUris"] = c.RedirectUris,
                }).ToList(),
                ["SeedAdmin"] = spec.SeedAdmin is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Email"] = spec.SeedAdmin.Email,
                    ["Password"] = spec.SeedAdmin.Password,
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
            ["ClientSecret"] = provider?.ClientSecret ?? string.Empty,
        };

    private static string BuildEnvExample(AuthSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Üretimde secret'ları appsettings yerine buradan (env override) verin.");
        sb.AppendLine("# Bu dosyayı .env olarak kopyalayın; docker-compose env_file ile okur.");
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
            sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Providers__{name}__ClientId=");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Auth__Providers__{name}__ClientSecret=");
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

    private static string BuildCompose(AuthSpec spec) =>
        $$"""
        # {{spec.Service}} merkez auth — izole test (servis + kendi PostgreSQL'i).
        #   docker compose up --build -d   ·   discovery: http://localhost:8081/.well-known/openid-configuration
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
            volumes:
              - {{spec.Service}}-pgdata:/var/lib/postgresql/data

          {{spec.Service}}:
            build: .
            environment:
              ASPNETCORE_ENVIRONMENT: Development
              Auth__Issuer: "http://{{spec.Service}}:8080/"
              ConnectionStrings__Default: "Host=postgres;Port=5432;Database={{spec.Database}};Username=baseforge;Password=change_me"
            ports:
              - "8081:8080"   # REST/OpenIddict (HTTP/1.1)
              - "8082:8081"   # gRPC (h2c) — merkez User entity'sine erişim
            depends_on:
              postgres:
                condition: service_healthy

        volumes:
          {{spec.Service}}-pgdata:

        """;

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
