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

    /// <summary>Kiracı (şirket) yönetimi (varsayılan: kapalı).</summary>
    public MultiTenancyOptions MultiTenancy { get; set; } = new();

    /// <summary>Tek kullanımlık kod: SMS ile giriş, telefon/e-posta doğrulama (varsayılan: kapalı).</summary>
    public OtpOptions Otp { get; set; } = new();
}

/// <summary>Tek kullanımlık kod (OTP) kuralları ve gönderici sağlayıcıları.</summary>
public sealed class OtpOptions
{
    /// <summary>Açık mı? Kapalıyken OTP uçları 404 döner, otp grant'ı reddedilir.</summary>
    public bool Enabled { get; set; }

    /// <summary>Kod uzunluğu (rakam).</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Kodun geçerlilik süresi (sn).</summary>
    public int LifetimeSeconds { get; set; } = 180;

    /// <summary>Bir koda en fazla hatalı deneme.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Aynı hedefe yeniden gönderim için bekleme (sn).</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Aynı hedefe saatte en fazla gönderim.</summary>
    public int MaxPerHour { get; set; } = 5;

    /// <summary>Ülke kodsuz yazılan telefonlar için (örn. 0555… → +90555…).</summary>
    public string DefaultCountryCode { get; set; } = "+90";

    /// <summary>SMS sağlayıcısı. Şimdilik yalnız <c>Log</c> (kod log'a yazılır, yalnız Development).</summary>
    public string SmsProvider { get; set; } = "Log";

    /// <summary>E-posta sağlayıcısı. Şimdilik yalnız <c>Log</c>.</summary>
    public string EmailProvider { get; set; } = "Log";
}

/// <summary>Kiracı (şirket) yönetimi: şirketler, üyelikler, token'da tenant_id.</summary>
public sealed class MultiTenancyOptions
{
    /// <summary>Açık mı? Kapalıyken kiracı uçları 404 döner, token'a tenant_id yazılmaz.</summary>
    public bool Enabled { get; set; }

    /// <summary>Şirketi açanın ve üye yönetenin rolü.</summary>
    public string AdminRole { get; set; } = "TenantAdmin";

    /// <summary>Şirket içinde verilebilen roller; yalnızca üyelikten gelir, global atanmaz.</summary>
    public List<string> MemberRoles { get; set; } = [];

    /// <summary>Self-servis açılan şirket onaylanana kadar beklemede kalır.</summary>
    public bool RequireApproval { get; set; } = true;

    /// <summary>Şirketleri onaylayan/askıya alan global roller.</summary>
    public List<string> ApproverRoles { get; set; } = ["SuperAdmin"];

    /// <summary>Admin rolü dahil tüm şirket içi roller.</summary>
    public IReadOnlyList<string> AllMemberRoles => [.. MemberRoles.Append(AdminRole).Distinct(StringComparer.Ordinal)];
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

    /// <summary>Kullanıcının kendine ekleyebileceği global roller (örn. şoför uygulaması için <c>Driver</c>).</summary>
    public List<string> SelfAssignableRoles { get; set; } = [];
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
