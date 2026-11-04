using System.Security.Cryptography;
using System.Text;

namespace BaseForge.CodeGen.Generation;

/// <summary>
/// Üretilen servisin secret dosyalarını yazar (bkz. docs/ARCH.md §6.5):
/// <list type="bullet">
/// <item><c>.env</c> — gerçek değerler (gitignore'lu). Yoksa rastgele bir Postgres şifresiyle oluşturulur; varsa
/// mevcut değerler korunur, yeniden üretim gerçek şifreyi asla ezmez.</item>
/// <item><c>.env.example</c> — format referansı, commit edilir.</item>
/// <item><c>.gitignore</c> — yoksa oluşturulur (<c>.env</c>, bin/obj).</item>
/// </list>
/// </summary>
internal static class ServiceSecrets
{
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    /// <summary>
    /// 0.6.2 ve öncesinde üretilmiş servislerde (Identity dahil) Postgres volume'u sabit <c>change_me</c> ile
    /// kurulmuştur. <c>.env</c>'de henüz <c>POSTGRES_PASSWORD</c> yoksa ve eski appsettings bu şifreyi taşıyorsa onu
    /// döner — rastgele şifreye geçmek mevcut veritabanına bağlantıyı koparırdı. Şifreyi değiştirmek kullanıcının
    /// bilinçli kararıdır (.env + volume sıfırlama). appsettings yeniden yazılmadan ÖNCE çağrılmalı.
    /// </summary>
    public static string? DetectLegacyPassword(string outputDir)
    {
        var appsettings = Path.Combine(outputDir, "appsettings.json");
        return !ReadEnv(Path.Combine(outputDir, ".env")).ContainsKey("POSTGRES_PASSWORD")
               && File.Exists(appsettings)
               && File.ReadAllText(appsettings).Contains("Password=change_me", StringComparison.Ordinal)
            ? "change_me"
            : null;
    }

    /// <summary>
    /// Kullanılacak Postgres şifresi: mevcut <c>.env</c>'deki değer (yeniden üretim asla ezmez), yoksa eski
    /// sürümden kalan <paramref name="legacyPassword"/>, o da yoksa yeni rastgele 32 karakter.
    /// </summary>
    public static string ResolvePostgresPassword(string outputDir, string? legacyPassword)
        => ReadEnv(Path.Combine(outputDir, ".env")).GetValueOrDefault("POSTGRES_PASSWORD") is { Length: > 0 } kept
            ? kept
            : legacyPassword ?? RandomNumberGenerator.GetString(PasswordAlphabet, 32);

    public static IEnumerable<string> Write(string outputDir, HostFileModel host, string? legacyPassword)
    {
        var envPath = Path.Combine(outputDir, ".env");
        var existing = ReadEnv(envPath);
        var postgresPassword = ResolvePostgresPassword(outputDir, legacyPassword);
        var rabbitPassword = existing.GetValueOrDefault("RABBITMQ_PASSWORD") ?? string.Empty;
        var serviceClientSecret = existing.GetValueOrDefault(ServiceClientSecretKey) ?? string.Empty;

        File.WriteAllText(envPath, BuildEnv(host, postgresPassword, rabbitPassword, serviceClientSecret, real: true));
        yield return envPath;

        var examplePath = Path.Combine(outputDir, ".env.example");
        File.WriteAllText(examplePath, BuildEnv(host, string.Empty, string.Empty, string.Empty, real: false));
        yield return examplePath;

        var gitignorePath = Path.Combine(outputDir, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            File.WriteAllText(gitignorePath, "bin/\nobj/\n**/bin/\n**/obj/\n.vs/\n*.user\n\n# Gerçek secret'lar — commit edilmez (bkz. .env.example)\n.env\n");
            yield return gitignorePath;
        }
    }

    private const string ServiceClientSecretKey = "Auth__ServiceClient__ClientSecret";

    private static string BuildEnv(HostFileModel host, string postgresPassword, string rabbitPassword, string serviceClientSecret, bool real)
    {
        var sb = new StringBuilder();
        sb.AppendLine(real
            ? "# GERÇEK secret'lar — commit ETMEYİN. Format referansı için .env.example'a bakın."
            : "# Format referansı — gerçek değerler '.env'de (üretici oluşturur, gitignore'lu). Bu dosya commit edilebilir.");
        sb.AppendLine("# docker-compose bu dosyadaki değişkenleri ${...} ile okur; yerel 'dotnet run' DotNetEnv ile yükler.");
        sb.AppendLine();
        sb.AppendLine("# Bu servisin PostgreSQL şifresi (container ilk oluşturulurken kullanılır; sonradan değiştirmek volume'u sıfırlamayı gerektirir).");
        sb.AppendLine("POSTGRES_PASSWORD=" + postgresPassword);
        sb.AppendLine("# Yerel 'dotnet run' bağlantısı (container içinde docker-compose kendi bağlantı dizesini verir).");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"ConnectionStrings__Default=Host=localhost;Port={host.PostgresPort};Database={host.Database};Username=baseforge;Password={postgresPassword}");
        if (host.HasRabbitMq)
        {
            sb.AppendLine();
            sb.AppendLine("# Paylaşılan RabbitMQ broker'ının şifresi — kökteki docker-compose.yml'ın RABBITMQ_PASSWORD'u ile AYNI olmalı.");
            sb.AppendLine("RABBITMQ_PASSWORD=" + rabbitPassword);
            sb.AppendLine("RabbitMq__Password=" + rabbitPassword);
        }

        if (host.HasAuth)
        {
            sb.AppendLine();
            sb.AppendLine("# Identity'deki '" + host.ServiceKey + "-service' client_credentials istemcisinin secret'ı (auth.yaml clients).");
            sb.AppendLine("# Boşsa arka plan işlerinden yapılan gRPC çağrıları token taşımaz (kullanıcı istekleri etkilenmez).");
            sb.AppendLine(ServiceClientSecretKey + "=" + serviceClientSecret);
        }

        return sb.ToString();
    }

    /// <summary>Basit <c>ANAHTAR=değer</c> okuyucu (yorum ve boş satırları atlar). Dosya yoksa boş.</summary>
    private static Dictionary<string, string> ReadEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            var eq = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (trimmed.Length == 0 || trimmed[0] == '#' || eq <= 0)
            {
                continue;
            }

            values[trimmed[..eq]] = trimmed[(eq + 1)..];
        }

        return values;
    }
}
