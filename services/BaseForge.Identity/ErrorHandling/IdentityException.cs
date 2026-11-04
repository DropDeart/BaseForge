using System.Net;

namespace BaseForge.Identity.ErrorHandling;

/// <summary>İş kuralı hatası; <see cref="IdentityExceptionMiddleware"/> ProblemDetails'e çevirir.</summary>
public class IdentityException(HttpStatusCode status, string errorCode, string message) : Exception(message)
{
    /// <summary>HTTP durum kodu.</summary>
    public HttpStatusCode Status { get; } = status;

    /// <summary>Makine okunur hata kodu (ProblemDetails.type).</summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>404.</summary>
    public static IdentityException NotFound(string message) => new(HttpStatusCode.NotFound, "not_found", message);

    /// <summary>403.</summary>
    public static IdentityException Forbidden(string message) => new(HttpStatusCode.Forbidden, "access.forbidden", message);

    /// <summary>409: kaydın durumu işleme uygun değil.</summary>
    public static IdentityException Conflict(string message) => new(HttpStatusCode.Conflict, "state.conflict", message);

    /// <summary>429: istek sınırı.</summary>
    public static IdentityException TooManyRequests(string message) => new(HttpStatusCode.TooManyRequests, "rate_limited", message);

    /// <summary>400.</summary>
    public static IdentityException Invalid(string message) => new(HttpStatusCode.BadRequest, "validation.failed", message);
}
