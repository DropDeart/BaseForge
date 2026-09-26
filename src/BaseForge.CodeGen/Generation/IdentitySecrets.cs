using System.Text.Json;
using BaseForge.CodeGen.Contracts;

namespace BaseForge.CodeGen.Generation;

/// <summary>
/// Identity secret'larının (seed admin parolası, client/provider secret'ları, Apple private key, imza
/// sertifikası parolası) tek kaynağı üretilen <c>.env</c>'dir (gitignore'lu). Diske yazılan <c>auth.yaml</c>
/// commit edilebilir olmalı; bu yüzden <see cref="Redacted"/> ile secret'sız kopyası yazılır. Kullanıcı bir
/// secret'ı boş bırakırsa (Designer formu secret'ları hep boş gösterir, CLI'da da auth.yaml artık boş taşır)
/// <see cref="RestoreUnchanged"/> mevcut değeri <c>.env</c>'den (eski projelerde auth.yaml'dan) geri koyar —
/// aksi halde yeniden üretim .env'deki gerçek parolayı boşla ezerdi.
/// </summary>
internal static class IdentitySecrets
{
    private static readonly JsonSerializerOptions CloneOptions = new();

    /// <summary>Secret alanları boşaltılmış bir kopya döndürür (girdi değişmez).</summary>
    public static AuthSpec Redacted(AuthSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var copy = JsonSerializer.Deserialize<AuthSpec>(JsonSerializer.Serialize(spec, CloneOptions), CloneOptions)!;

        if (copy.SeedAdmin is not null)
        {
            copy.SeedAdmin.Password = string.Empty;
        }

        foreach (var client in copy.Clients)
        {
            client.Secret = client.Public ? null : string.Empty;
        }

        foreach (var provider in Providers(copy).Select(p => p.Spec).OfType<ProviderSpec>())
        {
            provider.ClientSecret = string.Empty;
        }

        if (copy.Providers.Apple is not null)
        {
            copy.Providers.Apple.PrivateKey = string.Empty;
        }

        if (copy.Signing is not null)
        {
            copy.Signing.CertificatePassword = null;
        }

        return copy;
    }

    /// <summary>
    /// <paramref name="incoming"/>'de boş bırakılmış secret'ları, <paramref name="outputDir"/>'deki mevcut
    /// <c>.env</c>'den (yoksa eski biçimde secret taşıyan <c>auth.yaml</c>'dan) doldurur. Eşleştirme kimliğe göredir
    /// (seed admin e-postası, client/provider ClientId'si) — kimlik değiştiyse eski secret taşınmaz.
    /// </summary>
    public static void RestoreUnchanged(AuthSpec incoming, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        var authPath = Path.Combine(outputDir, "auth.yaml");
        if (!File.Exists(authPath))
        {
            return;
        }

        AuthSpec existing;
        try
        {
            existing = SpecLoader.Load<AuthSpec>(authPath);
        }
        catch
        {
            return;
        }

        var env = ReadEnv(Path.Combine(outputDir, ".env"));
        string? Pick(string? fromYaml, string envKey) =>
            !string.IsNullOrEmpty(fromYaml) ? fromYaml : env.GetValueOrDefault(envKey);

        if (incoming.SeedAdmin is { } seed && string.IsNullOrEmpty(seed.Password)
            && existing.SeedAdmin is { } oldSeed
            && string.Equals(oldSeed.Email, seed.Email, StringComparison.OrdinalIgnoreCase))
        {
            seed.Password = Pick(oldSeed.Password, "Auth__SeedAdmin__Password") ?? string.Empty;
        }

        foreach (var client in incoming.Clients.Where(c => !c.Public && string.IsNullOrEmpty(c.Secret)))
        {
            var oldIndex = existing.Clients.FindIndex(c => string.Equals(c.ClientId, client.ClientId, StringComparison.Ordinal));
            if (oldIndex >= 0)
            {
                client.Secret = Pick(existing.Clients[oldIndex].Secret, $"Auth__Clients__{oldIndex}__Secret");
            }
        }

        foreach (var ((name, provider), (_, oldProvider)) in Providers(incoming).Zip(Providers(existing)))
        {
            if (provider is not null && string.IsNullOrEmpty(provider.ClientSecret)
                && oldProvider is not null && string.Equals(oldProvider.ClientId, provider.ClientId, StringComparison.Ordinal))
            {
                provider.ClientSecret = Pick(oldProvider.ClientSecret, $"Auth__Providers__{name}__ClientSecret") ?? string.Empty;
            }
        }

        if (incoming.Providers.Apple is { } apple && string.IsNullOrEmpty(apple.PrivateKey)
            && existing.Providers.Apple is { } oldApple && string.Equals(oldApple.ClientId, apple.ClientId, StringComparison.Ordinal))
        {
            apple.PrivateKey = Pick(oldApple.PrivateKey, "Auth__Providers__Apple__PrivateKey") ?? string.Empty;
        }

        if (incoming.Signing is { } signing && string.IsNullOrEmpty(signing.CertificatePassword)
            && existing.Signing is { } oldSigning
            && string.Equals(oldSigning.CertificatePath, signing.CertificatePath, StringComparison.Ordinal))
        {
            signing.CertificatePassword = Pick(oldSigning.CertificatePassword, "Auth__SigningCertificatePassword");
        }
    }

    /// <summary>Üretilen dört klasik sağlayıcı — .env anahtar adıyla (Apple ayrı: statik secret yerine private key).</summary>
    internal static (string Name, ProviderSpec? Spec)[] Providers(AuthSpec spec) =>
    [
        ("Google", spec.Providers.Google),
        ("GitHub", spec.Providers.GitHub),
        ("Microsoft", spec.Providers.Microsoft),
        ("Facebook", spec.Providers.Facebook),
    ];

    /// <summary>Basit <c>KEY=VALUE</c> okuyucu (yorum/boş satırları atlar; değer ilk '='den sonrasının tamamı).</summary>
    private static Dictionary<string, string> ReadEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length == 0 || line[0] == '#' || eq <= 0)
            {
                continue;
            }

            values[line[..eq].Trim()] = line[(eq + 1)..];
        }

        return values;
    }
}
