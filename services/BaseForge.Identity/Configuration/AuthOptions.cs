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

    /// <summary>Seed edilecek roller. <c>Admin</c> ve <c>User</c> listede olmasa da her zaman oluşturulur.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Kendi kendine kayıt ayarları (varsayılan: kapalı).</summary>
    public RegistrationOptions Registration { get; set; } = new();

    /// <summary>Dış kimlik sağlayıcıları (Google/GitHub/Microsoft/Facebook/Apple).</summary>
    public ProvidersOptions Providers { get; set; } = new();
}

/// <summary>Kendi kendine kayıt ayarları (bkz. BaseForge docs/ARCH.md §6.1).</summary>
public sealed class RegistrationOptions
{
    /// <summary>
    /// Kayıt açık mı? Kapalıyken <c>/api/account/register</c> 404 döner ve dış sağlayıcıyla ilk kez gelen
    /// kullanıcı için hesap oluşturulmaz — yalnızca önceden var olan kullanıcılar giriş yapabilir.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Kayıt olan (veya dış sağlayıcıyla ilk kez gelen) kullanıcıya verilecek rol.</summary>
    public string DefaultRole { get; set; } = "User";
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

    /// <summary>Sign in with Apple — diğerlerinden farklı bir kimlik bilgisi şekli (bkz. <see cref="AppleProviderOptions"/>).</summary>
    public AppleProviderOptions? Apple { get; set; }
}

/// <summary>Bir dış sağlayıcının client kimlik bilgileri (secret'lar env'den gelmeli).</summary>
public sealed class ExternalProviderOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// Sign in with Apple kimlik bilgileri. Diğer sağlayıcılar gibi tek bir ClientId/ClientSecret çifti
/// değil — Apple'da "client secret" statik değil, TeamId/KeyId/PrivateKey ile her seferinde imzalanan
/// bir JWT'dir (bkz. AspNet.Security.OAuth.Apple paketinin GenerateClientSecret'ı).
/// </summary>
public sealed class AppleProviderOptions
{
    /// <summary>Apple Developer portalındaki "Services ID" (örn. <c>com.hekimburada.web</c>).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Apple Developer hesabının Team ID'si.</summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>İmzalama için kullanılan private key'in Key ID'si.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// PKCS#8 formatındaki private key (.p8 dosyasının içeriği). env'de tek satıra sığdırmak için
    /// gerçek satır sonları yerine <c>\n</c> kaçış dizisi kullanılabilir — <c>ExternalProviders</c>
    /// bunu okurken geri çevirir.
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;
}
