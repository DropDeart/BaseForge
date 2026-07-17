namespace BaseForge.CodeGen.Contracts;

/// <summary>Merkez auth (Identity) servisinin deklaratif tanımı (auth.yaml).</summary>
public sealed class AuthSpec
{
    /// <summary>Servis adı (örn. <c>identity</c>). Proje/namespace bundan türetilir.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Identity servisinin kendi veritabanı adı.</summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>JWT/OAuth2 issuer adresi.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token imzalama sertifikası ayarları (opsiyonel — verilmezse kütüphane varsayılanı kullanılır).</summary>
    public SigningSpec? Signing { get; set; }

    /// <summary>Tanımlı OAuth2/OIDC scope'ları.</summary>
    public List<AuthScopeSpec> Scopes { get; set; } = [];

    /// <summary>Tanımlı OAuth2 client'ları (SPA, servis-servis, vb.).</summary>
    public List<AuthClientSpec> Clients { get; set; } = [];

    /// <summary>Seed edilecek ilk admin kullanıcısı (opsiyonel).</summary>
    public SeedAdminSpec? SeedAdmin { get; set; }

    /// <summary>Harici oturum açma sağlayıcıları (Google/GitHub/Microsoft/Facebook).</summary>
    public ProvidersSpec Providers { get; set; } = new();

    /// <summary>Docker host portları (opsiyonel). Boş alanlar için varsayılanlar kullanılır.</summary>
    public DockerPortsSpec? DockerPorts { get; set; }

    /// <summary>
    /// SPA'lardan (farklı origin) çağrılabilmesi için izinli origin'ler (örn. <c>http://localhost:5173</c>).
    /// appsettings.json'da <c>Cors:AllowedOrigins</c> olarak üretilir; boşsa CORS devre dışı kalır.
    /// </summary>
    public List<string> CorsOrigins { get; set; } = [];
}

/// <summary>Token imzalama sertifikası ayarları.</summary>
public sealed class SigningSpec
{
    /// <summary>İmzalama sertifikasının (.pfx) dosya yolu.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>İmzalama sertifikasının parolası.</summary>
    public string? CertificatePassword { get; set; }
}

/// <summary>Bir OAuth2/OIDC scope tanımı.</summary>
public sealed class AuthScopeSpec
{
    /// <summary>Scope adı (örn. <c>orders.read</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Scope'un ait olduğu kaynak (opsiyonel, belgeleme amaçlı).</summary>
    public string? Resource { get; set; }
}

/// <summary>Bir OAuth2 client tanımı.</summary>
public sealed class AuthClientSpec
{
    /// <summary>Client kimliği.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret (public/SPA client'lar için boş bırakılır).</summary>
    public string? Secret { get; set; }

    /// <summary>Public client mı (PKCE, secret'sız)?</summary>
    public bool Public { get; set; }

    /// <summary>İzinli OAuth2 grant tipleri (örn. <c>authorization_code</c>, <c>client_credentials</c>).</summary>
    public List<string> Grants { get; set; } = [];

    /// <summary>Client'a tanınan scope'lar.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>İzinli redirect URI'ları.</summary>
    public List<string> RedirectUris { get; set; } = [];
}

/// <summary>Seed edilecek ilk admin kullanıcısı.</summary>
public sealed class SeedAdminSpec
{
    /// <summary>Admin kullanıcısının e-posta adresi.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Admin kullanıcısının parolası (ASP.NET Identity parola politikasına uymalı).</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Harici oturum açma sağlayıcıları.</summary>
public sealed class ProvidersSpec
{
    /// <summary>Google OAuth ayarları (opsiyonel).</summary>
    public ProviderSpec? Google { get; set; }

    /// <summary>GitHub OAuth ayarları (opsiyonel).</summary>
    public ProviderSpec? GitHub { get; set; }

    /// <summary>Microsoft OAuth ayarları (opsiyonel).</summary>
    public ProviderSpec? Microsoft { get; set; }

    /// <summary>Facebook OAuth ayarları (opsiyonel).</summary>
    public ProviderSpec? Facebook { get; set; }
}

/// <summary>Tek bir harici OAuth sağlayıcısının client kimlik bilgileri.</summary>
public sealed class ProviderSpec
{
    /// <summary>Sağlayıcıdan alınan client kimliği.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Sağlayıcıdan alınan client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}
