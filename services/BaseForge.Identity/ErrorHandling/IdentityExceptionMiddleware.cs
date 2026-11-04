using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.ErrorHandling;

/// <summary>Hataları BaseForge servisleriyle aynı biçimde ProblemDetails olarak döner.</summary>
public sealed partial class IdentityExceptionMiddleware(RequestDelegate next, ILogger<IdentityExceptionMiddleware> logger)
{
    /// <summary>İsteği işler, hatayı yakalar.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Eşzamanlı aynı kayıt (ör. aynı şirkete iki davet): 500 değil 409.
            LogRuleViolation(logger, "db.unique_violation", ex.InnerException.Message);
            await WriteAsync(context, HttpStatusCode.Conflict, "db.unique_violation", "Bu kayıt zaten var.");
        }
        catch (IdentityException ex)
        {
            LogRuleViolation(logger, ex.ErrorCode, ex.Message);
            await WriteAsync(context, ex.Status, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            LogUnhandled(logger, ex);
            await WriteAsync(context, HttpStatusCode.InternalServerError, "internal_error", "Beklenmeyen bir hata oluştu.");
        }
    }

    private static Task WriteAsync(HttpContext context, HttpStatusCode status, string errorCode, string title)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = (int)status, Title = title, Type = errorCode });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "İş kuralı hatası: {ErrorCode} {Detail}")]
    private static partial void LogRuleViolation(ILogger logger, string errorCode, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "İşlenmeyen istisna oluştu.")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
