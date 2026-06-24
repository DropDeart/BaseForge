using BaseForge.API.Middleware;
using Microsoft.AspNetCore.Builder;

namespace BaseForge.API.Extensions;

/// <summary>
/// BaseForge HTTP pipeline bileşenlerini ekleyen extension metotları.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// BaseForge middleware'lerini pipeline'a ekler: önce exception handling, ardından request logging.
    /// Pipeline'ın başında çağrılmalıdır. JWT kullanılıyorsa, uygulamanın ayrıca
    /// <c>UseAuthentication()</c>/<c>UseAuthorization()</c> çağırması gerekir.
    /// </summary>
    /// <param name="app">Uygulama pipeline'ı.</param>
    /// <returns>Zincirleme için aynı <paramref name="app"/>.</returns>
    public static IApplicationBuilder UseBaseForge(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        return app;
    }
}
