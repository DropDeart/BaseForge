using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.Controllers;

/// <summary>
/// Tüm API controller'ları için temel sınıf. MediatR <see cref="ISender"/>'ına kısa erişim sağlar.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    private ISender? _mediator;

    /// <summary>
    /// Command/query göndermek için MediatR sender'ı. İlk erişimde DI'dan çözülür.
    /// </summary>
    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>
    /// İsteği yapan kullanıcının kimliği (JWT <c>sub</c> claim'i); kimlik doğrulanmamışsa veya
    /// <c>sub</c> bir <see cref="Guid"/> değilse <see langword="null"/>.
    /// </summary>
    protected Guid? CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    /// <summary>
    /// İsteği yapan kullanıcı verilen rollerden en az birine sahip mi? Rol claim'i hem kısa
    /// (<c>role</c>, OpenIddict) hem uzun (<see cref="ClaimTypes.Role"/>) adıyla aranır; böylece
    /// <c>EnableJwt</c> dışında kurulmuş kimlik doğrulama şemalarında da doğru çalışır.
    /// </summary>
    /// <param name="roles">Aranan rol adları (büyük/küçük harf duyarlı, Identity'deki adlarla aynı).</param>
    protected bool IsInAnyRole(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return User.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && roles.Contains(c.Value, StringComparer.Ordinal));
    }

    /// <summary>
    /// "Sahibi veya şu roller" kuralı için sorgu/komuta geçirilecek sahiplik kısıtı: çağıran
    /// <paramref name="bypassRoles"/>'dan birine sahipse <see langword="null"/> (kısıtlama yok), değilse
    /// kendi kimliği — kimliği yoksa hiçbir kayıtla eşleşmeyen <see cref="Guid.Empty"/> (bkz. docs/ARCH.md §6.1).
    /// </summary>
    /// <param name="bypassRoles">Sahiplik kontrolünü geçen roller (kuraldaki roller + superRoles).</param>
    protected Guid? OwnerRestriction(params string[] bypassRoles) =>
        IsInAnyRole(bypassRoles) ? null : CurrentUserId ?? Guid.Empty;
}
