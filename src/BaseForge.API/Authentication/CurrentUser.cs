using System.Security.Claims;
using BaseForge.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BaseForge.API.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/>'ın HttpContext/JWT claim'leri üzerinden çalışan implementasyonu.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Verilen <see cref="IHttpContextAccessor"/> ile yeni bir örnek oluşturur.</summary>
    /// <param name="httpContextAccessor">Aktif HTTP bağlamına erişim.</param>
    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}
