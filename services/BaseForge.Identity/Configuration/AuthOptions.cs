namespace BaseForge.Identity.Configuration;

/// <summary>
/// Merkez auth'un deklaratif yapılandırması (appsettings <c>Auth</c> bölümü + env).
/// Client'lar, scope'lar, seed admin ve imzalama buradan okunur — kod sabit değildir.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Sabit token issuer'ı (örn. <c>https://identity.firma.com/</c>).</summary>
    public string? Issuer { get; set; }

    /// <summary>İmza sertifikası (.pfx) yolu. Boşsa geliştirme için ephemeral RSA anahtarı kullanılır.</summary>
    public string? SigningCertificatePath { get; set; }

    /// <summary>İmza sertifikası parolası.</summary>
    public string? SigningCertificatePassword { get; set; }

    /// <summary>Tanımlı API scope'ları.</summary>
    public List<ScopeOptions> Scopes { get; set; } = [];

    /// <summary>OAuth2 client tanımları.</summary>
    public List<ClientOptions> Clients { get; set; } = [];

    /// <summary>İlk açılışta oluşturulacak admin kullanıcı.</summary>
    public SeedAdminOptions? SeedAdmin { get; set; }

    /// <summary>Dış kimlik sağlayıcıları (Google/GitHub/Microsoft/Facebook). Faz P3'te devreye girer.</summary>
    public ProvidersOptions Providers { get; set; } = new();
}

/// <summary>Bir API scope'u ve eşlendiği kaynak (audience).</summary>
public sealed class ScopeOptions
{
    public string Name { get; set; } = string.Empty;

    public string? Resource { get; set; }
}

/// <summary>Bir OAuth2 client tanımı.</summary>
public sealed class ClientOptions
{
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gizli (confidential) client secret'ı. Public client'larda boş bırakılır.</summary>
    public string? Secret { get; set; }

    /// <summary>Public (SPA/mobil, secret'sız) client mı?</summary>
    public bool Public { get; set; }

    /// <summary>İzinli grant'lar: password, client_credentials, refresh_token, authorization_code.</summary>
    public List<string> Grants { get; set; } = [];

    /// <summary>Client'ın erişebileceği scope'lar.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>authorization_code için izinli redirect URI'leri.</summary>
    public List<string> RedirectUris { get; set; } = [];
}

/// <summary>Seed admin kullanıcı bilgileri.</summary>
public sealed class SeedAdminOptions
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

/// <summary>Dış kimlik sağlayıcı ayarları (her biri opsiyonel).</summary>
public sealed class ProvidersOptions
{
    public ExternalProviderOptions? Google { get; set; }

    public ExternalProviderOptions? GitHub { get; set; }

    public ExternalProviderOptions? Microsoft { get; set; }

    public ExternalProviderOptions? Facebook { get; set; }
}

/// <summary>Bir dış sağlayıcının client kimlik bilgileri (secret'lar env'den gelmeli).</summary>
public sealed class ExternalProviderOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
