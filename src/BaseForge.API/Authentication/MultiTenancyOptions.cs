namespace BaseForge.API.Authentication;

/// <summary>Kiracı ayarları (<c>EnableMultiTenancy</c>).</summary>
public sealed class MultiTenancyOptions
{
    /// <summary>
    /// Token'ında kiracı olmayan bu rollerdeki kullanıcılar GET/HEAD isteklerinde tüm kiracıların
    /// kayıtlarını görür (örn. SuperAdmin raporları). Yazma işlemleri yine kiracı ister.
    /// </summary>
    public IReadOnlyList<string> CrossTenantRoles { get; set; } = [];
}
