using System.Globalization;
using System.Security.Cryptography;
using BaseForge.CodeGen.Generation;

namespace BaseForge.CodeGen.Designer;

/// <summary>Designer generate korumaları: bayat formla üzerine yazma (DES-1) ve port çakışması (DES-4).</summary>
internal static class SpecFileGuard
{
    /// <summary>Dosya içeriğinin SHA-256'sı; dosya yoksa null.</summary>
    public static string? Hash(string path)
        => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;

    /// <summary>Form açıldığından beri dosya değişti mi (ya da form açılırken yoktu, şimdi var)?</summary>
    public static bool IsStale(string path, string? baseHash) => Hash(path) is { } current && current != baseHash;

    /// <summary>Kullanıcıya gösterilen uyarı.</summary>
    public static string StaleMessage(string fileName)
        => $"{fileName} bu ekran açıldıktan sonra diskte değişmiş. Üretim ekrandaki hâliyle dosyanın üzerine yazar; önce sayfayı yenileyin ya da bilerek üzerine yazın.";

    /// <summary>Workspace'teki diğer servislerle aynı host portunu kullanan alanlar.</summary>
    public static List<string> PortConflicts(IReadOnlyList<ServiceRegistryEntry> workspace, string service, int rest, int grpc, int postgres)
    {
        var errors = new List<string>();
        foreach (var other in workspace.Where(e => !string.Equals(e.Name, service, StringComparison.OrdinalIgnoreCase)))
        {
            var taken = new[] { ("REST", other.RestPort), ("gRPC", other.GrpcPort), ("PostgreSQL", other.PostgresPort) }
                .Where(p => p.Item2 is not null)
                .ToList();
            foreach (var (label, port) in new[] { ("REST", rest), ("gRPC", grpc), ("PostgreSQL", postgres) })
            {
                var clash = taken.FirstOrDefault(t => t.Item2 == port);
                if (clash.Item2 is not null)
                {
                    errors.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} portu {1}, '{2}' servisinin {3} portuyla çakışıyor (dockerPorts ile değiştirin).", label, port, other.Name, clash.Item1));
                }
            }
        }

        return errors;
    }
}
