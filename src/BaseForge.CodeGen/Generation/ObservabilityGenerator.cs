using System.Reflection;
using System.Security.Cryptography;

namespace BaseForge.CodeGen.Generation;

/// <summary>
/// Workspace köküne (üretilen servislerin yanına) paylaşılan bir <c>observability/</c> klasörü yazar: Grafana Loki
/// + Grafana (hazır Loki veri kaynağı ve BaseForge log dashboard'u). Üretilen servislerin varsayılan
/// <c>Serilog:LokiUrl</c>'i (<c>http://host.docker.internal:3100</c>) buraya bağlanır — önceden Loki yalnızca
/// BaseForge deposunda tanımlıydı, kullanıcının workspace'inde çalışan bir Loki olmadığı için loglar sessizce
/// yalnızca konsola düşüyordu. Klasör zaten varsa hiç dokunulmaz (kullanıcı retention/port ayarlamış olabilir).
/// </summary>
internal static class ObservabilityGenerator
{
    private const string FolderName = "observability";
    private const string ResourcePrefix = "observability/";

    /// <summary>Servis/identity çıktı klasörünün bir üstüne (workspace kökü) klasörü yoksa üretir; yazılan dosyaları döndürür.</summary>
    public static IReadOnlyList<string> EnsureForWorkspace(string serviceOutputDir)
    {
        var full = Path.GetFullPath(serviceOutputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var workspaceRoot = Path.GetDirectoryName(full) ?? full;
        var dir = Path.Combine(workspaceRoot, FolderName);
        if (Directory.Exists(dir))
        {
            return [];
        }

        var written = new List<string>();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            var path = Path.Combine(workspaceRoot, resource.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = File.Create(path);
            stream.CopyTo(file);
            written.Add(path);
        }

        written.Add(Write(dir, "docker-compose.yml", Compose));
        // Grafana admin parolası rastgele üretilir ve yalnızca .env'de durur (commit edilmez).
        written.Add(Write(dir, ".env", $"GRAFANA_USER=admin\nGRAFANA_PASSWORD={RandomPassword()}\nLOKI_PORT=3100\nGRAFANA_PORT=3000\n"));
        written.Add(Write(dir, ".env.example", "GRAFANA_USER=admin\nGRAFANA_PASSWORD=\nLOKI_PORT=3100\nGRAFANA_PORT=3000\n"));
        written.Add(Write(dir, ".gitignore", ".env\n"));
        return written;
    }

    private static string Write(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string RandomPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
        .Replace('+', 'x').Replace('/', 'y');

    private const string Compose =
        """
        # Merkezi loglama — bu workspace'teki tüm BaseForge servisleri (Serilog) loglarını buraya gönderir.
        # Başlat:  docker compose up -d        (bu klasörde)
        # Grafana: http://localhost:3000  (kullanıcı/parola: .env içindeki GRAFANA_USER / GRAFANA_PASSWORD)
        #          'BaseForge Logs' dashboard'u ve Loki veri kaynağı hazır gelir.
        # Servisler Loki'ye appsettings.json'daki Serilog:LokiUrl ile bağlanır (varsayılan
        # http://host.docker.internal:3100); Loki kapalıysa servisler sessizce yalnızca konsola loglar.
        # Log saklama süresi (varsayılan 7 gün): grafana/loki-config.yaml -> limits_config.retention_period
        name: baseforge-observability
        services:
          loki:
            image: grafana/loki:3.2.0
            ports:
              - "${LOKI_PORT:-3100}:3100"
            volumes:
              - ./grafana/loki-config.yaml:/etc/loki/loki-config.yaml
              - loki_data:/loki
            command: -config.file=/etc/loki/loki-config.yaml
            restart: unless-stopped

          grafana:
            image: grafana/grafana:11.4.0
            environment:
              GF_SECURITY_ADMIN_USER: ${GRAFANA_USER:-admin}
              GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD:?GRAFANA_PASSWORD .env içinde tanımlı olmalı}
            ports:
              - "${GRAFANA_PORT:-3000}:3000"
            volumes:
              - ./grafana/provisioning:/etc/grafana/provisioning
              - ./grafana/dashboards:/etc/grafana/dashboards
              - grafana_data:/var/lib/grafana
            depends_on:
              - loki
            restart: unless-stopped

        volumes:
          loki_data:
          grafana_data:

        """;
}
