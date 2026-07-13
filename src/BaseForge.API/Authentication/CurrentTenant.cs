using BaseForge.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BaseForge.API.Authentication;

/// <summary>
/// <see cref="ICurrentTenant"/>'ın HttpContext/JWT claim'i üzerinden çalışan implementasyonu.
/// Claim adı sabit: <c>tenant_id</c>.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Verilen <see cref="IHttpContextAccessor"/> ile yeni bir örnek oluşturur.</summary>
    /// <param name="httpContextAccessor">Aktif HTTP bağlamına erişim.</param>
    public CurrentTenant(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : null;
        }
    }
}
