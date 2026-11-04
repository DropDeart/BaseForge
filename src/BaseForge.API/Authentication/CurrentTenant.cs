using BaseForge.Core.Interfaces;
using BaseForge.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace BaseForge.API.Authentication;

/// <summary>
/// <see cref="ICurrentTenant"/>'ın implementasyonu. Kiracı şu sırayla çözülür (bkz. docs/ARCH.md §5.5):
/// <list type="number">
/// <item>Kullanıcı token'ındaki <c>tenant_id</c> claim'i.</item>
/// <item>Ortam kapsamı (<see cref="TenantScope"/>) — RabbitMQ tüketicisi / arka plan işi.</item>
/// <item>Yalnızca servis token'ı (<c>bf_svc</c> claim'i) ile gelen istekte <c>x-tenant-id</c> başlığı/metadata'sı.</item>
/// </list>
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
            var context = _httpContextAccessor.HttpContext;
            if (Guid.TryParse(context?.User.FindFirst(BaseForgeClaims.TenantId)?.Value, out var fromClaim))
            {
                return fromClaim;
            }

            if (TenantScope.Current is { } ambient)
            {
                return ambient;
            }

            return context?.User.HasClaim(BaseForgeClaims.ServiceToken, "1") == true
                   && Guid.TryParse(context.Request.Headers[BaseForgeClaims.TenantIdHeader], out var fromService)
                ? fromService
                : null;
        }
    }
}
