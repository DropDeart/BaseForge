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
}
