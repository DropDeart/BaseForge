using System.Net;
using BaseForge.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BaseForge.API.Middleware;

/// <summary>
/// Pipeline'da oluşan istisnaları yakalayıp standart <see cref="ProblemDetails"/> yanıtına çevirir.
/// <see cref="BaseException"/> türevleri uygun HTTP durum koduna eşlenir; diğerleri 500 olur.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Yeni bir middleware örneği oluşturur.</summary>
    /// <param name="next">Pipeline'daki bir sonraki bileşen.</param>
    /// <param name="logger">Logger.</param>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    /// <summary>İsteği işler ve oluşan istisnaları yakalar.</summary>
    /// <param name="context">HTTP bağlamı.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, errorCode) = exception switch
        {
            NotFoundException ex => (HttpStatusCode.NotFound, ex.Message, ex.ErrorCode),
            ValidationException ex => (HttpStatusCode.BadRequest, ex.Message, ex.ErrorCode),
            BaseException ex => (HttpStatusCode.BadRequest, ex.Message, ex.ErrorCode),
            _ => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu.", "internal_error"),
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "İşlenmeyen istisna oluştu.");
        }
        else
        {
            _logger.LogWarning(exception, "Domain istisnası işlendi: {ErrorCode}", errorCode);
        }

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = errorCode,
        };

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem).ConfigureAwait(false);
    }
}
