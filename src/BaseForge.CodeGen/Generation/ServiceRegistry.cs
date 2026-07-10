using System.Text.Json;
using System.Text.Json.Serialization;
using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>
/// Servisler birbirinden izole Docker container'larında çalıştığı için (paylaşılan dosya sistemi yok),
/// "hangi servis identity'ye bağlı" bilgisini tek bir yerde toplamanın yolu yok — her servis bunu
/// sadece kendi <c>spec.yaml</c>'inde bilir. Bu sınıf, üretim sırasında workspace kökünde
/// (üretilen servis klasörünün bir üstünde) paylaşılan bir <c>services.json</c> kaydı tutar;
/// identity üretilirken bu kaydın güncel hali kendi <c>wwwroot</c>'una kopyalanıp imaja gömülür,
/// dashboard'un "Servisler" bölümü onu okur.
/// </summary>
internal static class ServiceRegistry
{
    private const string FileName = "services.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void UpsertService(string outputDir, ServiceSpec spec)
    {
        var entry = new ServiceRegistryEntry(
            Name: spec.Service,
            RestPort: spec.DockerPorts?.Rest,
            GrpcPort: spec.DockerPorts?.Grpc,
            EntityCount: spec.Entities.Count,
            IsIdentity: false,
            Authority: spec.Auth?.Authority,
            Audience: spec.Auth?.Audience,
            Protected: spec.Auth?.Protect ?? false);

        Upsert(WorkspaceRoot(outputDir), entry);
    }

    public static void UpsertIdentity(string outputDir, AuthSpec spec)
    {
        var entry = new ServiceRegistryEntry(
            Name: spec.Service,
            RestPort: spec.DockerPorts?.Rest ?? 8081,
            GrpcPort: spec.DockerPorts?.Grpc ?? 8082,
            EntityCount: null,
            IsIdentity: true,
            Authority: null,
            Audience: null,
            Protected: false);

        Upsert(WorkspaceRoot(outputDir), entry);
    }

    /// <summary>Workspace kökündeki güncel kaydı identity'nin kendi wwwroot'una (Docker imajına gömülsün diye) kopyalar.</summary>
    public static void SnapshotForIdentity(string identityOutputDir)
    {
        var source = Path.Combine(WorkspaceRoot(identityOutputDir), FileName);
        var destDir = Path.Combine(identityOutputDir, "wwwroot");
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, FileName);

        if (File.Exists(source))
        {
            File.Copy(source, dest, overwrite: true);
        }
        else
        {
            File.WriteAllText(dest, JsonSerializer.Serialize(new ServiceRegistryFile([]), JsonOptions));
        }
    }

    private static string WorkspaceRoot(string outputDir)
    {
        var full = Path.GetFullPath(outputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.GetDirectoryName(full) ?? full;
    }

    private static void Upsert(string workspaceRoot, ServiceRegistryEntry entry)
    {
        Directory.CreateDirectory(workspaceRoot);
        var path = Path.Combine(workspaceRoot, FileName);

        var list = Load(path);
        list.RemoveAll(e => string.Equals(e.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
        list.Add(entry);
        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        File.WriteAllText(path, JsonSerializer.Serialize(new ServiceRegistryFile(list), JsonOptions));
    }

    private static List<ServiceRegistryEntry> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var file = JsonSerializer.Deserialize<ServiceRegistryFile>(File.ReadAllText(path), JsonOptions);
            return file?.Services.ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

internal sealed record ServiceRegistryFile(List<ServiceRegistryEntry> Services);

public sealed record ServiceRegistryEntry(
    string Name,
    int? RestPort,
    int? GrpcPort,
    int? EntityCount,
    bool IsIdentity,
    string? Authority,
    string? Audience,
    bool Protected);
