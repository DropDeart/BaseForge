using BaseForge.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.Extensions;

/// <summary>
/// BaseForge HTTP pipeline bileşenlerini ekleyen extension metotları.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// BaseForge middleware'lerini pipeline'a ekler: correlation id, exception handling,
    /// request logging, <c>/health</c> endpoint'i ve (AddBaseForge'da JWT etkinleştirildiyse)
    /// authentication + authorization. Pipeline'ın başında, <c>MapControllers</c>'tan önce çağrılmalıdır.
    /// </summary>
    /// <param name="app">Uygulama pipeline'ı (endpoint eşleme gerektiği için <see cref="WebApplication"/>).</param>
    /// <returns>Zincirleme için aynı <paramref name="app"/>.</returns>
    public static WebApplication UseBaseForge(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // En başta: sonraki tüm middleware'lerin (exception handling, request logging) ve
        // handler'ların logları doğru CorrelationId'yi taşısın diye.
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        var features = app.Services.GetService<BaseForgeFeatures>();
        if (features?.JwtEnabled == true)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        // JWT etkinse bile kimlik doğrulaması gerektirmez — Identity'nin ve orkestrasyon
        // araçlarının (Docker healthcheck) her zaman erişebilmesi gerekir.
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthReportAsync });

        return app;
    }

    private static Task WriteHealthReportAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        });
}
