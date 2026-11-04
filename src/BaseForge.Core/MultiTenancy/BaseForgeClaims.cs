namespace BaseForge.Core.MultiTenancy;

/// <summary>BaseForge'un token ve metadata sözleşmesindeki sabit adlar (Identity ve servisler paylaşır).</summary>
public static class BaseForgeClaims
{
    /// <summary>Kullanıcının o anki kiracısı (JWT claim'i).</summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// Servis-servis (client_credentials) token'ı işareti; değeri <c>"1"</c>. Yalnızca bu claim'i taşıyan çağrılarda
    /// kiracı <see cref="TenantIdHeader"/>'dan okunur — kullanıcı token'ıyla gelen başlık yok sayılır (taklit edilemez).
    /// </summary>
    public const string ServiceToken = "bf_svc";

    /// <summary>Servis token'ıyla yapılan çağrıda kiracıyı taşıyan HTTP başlığı / gRPC metadata anahtarı.</summary>
    public const string TenantIdHeader = "x-tenant-id";
}
