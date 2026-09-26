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

    /// <summary>
    /// Seed edilecek roller (örn. <c>[Editor, SuperAdmin]</c>). <c>Admin</c> ve <c>User</c> her zaman
    /// oluşturulur; buraya yazılması gerekmez. Servis spec'lerindeki <c>access</c> rolleri bu listeyle
    /// karşılaştırılır (bkz. docs/ARCH.md §6.1).
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Kullanıcıların kendi kendine kayıt olup olamayacağı (varsayılan: kapalı).</summary>
    public RegistrationSpec Registration { get; set; } = new();

    /// <summary>Harici oturum açma sağlayıcıları (Google/GitHub/Microsoft/Facebook).</summary>
    public ProvidersSpec Providers { get; set; } = new();

    /// <summary>Docker host portları (opsiyonel). Boş alanlar için varsayılanlar kullanılır.</summary>
    public DockerPortsSpec? DockerPorts { get; set; }

    /// <summary>
    /// SPA'lardan (farklı origin) çağrılabilmesi için izinli origin'ler (örn. <c>http://localhost:5173</c>).
    /// appsettings.json'da <c>Cors:AllowedOrigins</c> olarak üretilir; boşsa CORS devre dışı kalır.
    /// </summary>
    public List<string> CorsOrigins { get; set; } = [];

    /// <summary>
    /// Kullanıcıya (<c>ApplicationUser</c>) eklenecek domain'e özgü profil alanları (opsiyonel). Bkz. docs/ARCH.md §6.3.
    /// </summary>
    public UserProfileSpec? UserProfile { get; set; }
}

/// <summary>Kullanıcı profil alanları (bkz. <see cref="AuthSpec.UserProfile"/>).</summary>
public sealed class UserProfileSpec
{
    /// <summary>Yalnızca admin panelinden düzenlenebilen alanlar için <see cref="PropSpec.EditableBy"/> değeri.</summary>
    public const string EditableByAdmin = "admin";

    /// <summary>Kullanıcının kendi profilinden düzenleyebildiği alanlar için <see cref="PropSpec.EditableBy"/> değeri (varsayılan).</summary>
    public const string EditableBySelf = "self";

    /// <summary>
    /// Alanlar: ad → tanım (servis <c>props</c>'uyla aynı biçim) + <c>editableBy</c> (<c>self</c>|<c>admin</c>) ve
    /// <c>inToken</c>. Alanlar doğrudan <c>ApplicationUser</c>'a eklenir (ayrı tablo yok).
    /// </summary>
    public Dictionary<string, PropSpec> Props { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Alan yalnızca admin tarafından mı düzenlenebilir?</summary>
    public static bool IsAdminOnly(PropSpec prop)
    {
        ArgumentNullException.ThrowIfNull(prop);
        return string.Equals(prop.EditableBy, EditableByAdmin, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Kendi kendine kayıt ayarları (bkz. docs/ARCH.md §6.1).</summary>
public sealed class RegistrationSpec
{
    /// <summary>Her zaman var olan varsayılan kullanıcı rolü.</summary>
    public const string DefaultUserRole = "User";

    /// <summary>
    /// Kayıt açık mı? Kapalıyken <c>/api/account/register</c> 404 döner ve dış sağlayıcıyla (Google vb.)
    /// ilk kez gelen kullanıcı için hesap oluşturulmaz. Varsayılan <see langword="false"/> (güvenli varsayılan).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Kayıt olan (veya dış sağlayıcıyla ilk kez gelen) kullanıcıya verilecek rol.</summary>
    public string DefaultRole { get; set; } = DefaultUserRole;
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

    /// <summary>Sign in with Apple ayarları (opsiyonel) — diğerlerinden farklı bir kimlik bilgisi şekli, bkz. <see cref="AppleProviderSpec"/>.</summary>
    public AppleProviderSpec? Apple { get; set; }
}

/// <summary>Tek bir harici OAuth sağlayıcısının client kimlik bilgileri.</summary>
public sealed class ProviderSpec
{
    /// <summary>Sağlayıcıdan alınan client kimliği.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Sağlayıcıdan alınan client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// Sign in with Apple kimlik bilgileri. Apple'da statik bir "client secret" yok — TeamId/KeyId/
/// PrivateKey'den her seferinde imzalanan bir JWT üretilir (bkz. services/BaseForge.Identity/
/// Authentication/ExternalProviders.cs).
/// </summary>
public sealed class AppleProviderSpec
{
    /// <summary>Apple Developer portalındaki "Services ID" (örn. <c>com.hekimburada.web</c>).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Apple Developer hesabının Team ID'si.</summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>İmzalama için kullanılan private key'in Key ID'si.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>PKCS#8 formatındaki private key (.p8 dosyasının içeriği, gerçek satır sonu veya <c>\n</c> kaçış dizisiyle).</summary>
    public string PrivateKey { get; set; } = string.Empty;
}
