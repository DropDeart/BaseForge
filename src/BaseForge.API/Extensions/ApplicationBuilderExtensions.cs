using BaseForge.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.Extensions;

/// <summary>
/// BaseForge HTTP pipeline bileşenlerini ekleyen extension metotları.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// BaseForge middleware'lerini pipeline'a ekler: correlation id, exception handling,
    /// request logging ve (AddBaseForge'da JWT etkinleştirildiyse) authentication + authorization.
    /// Pipeline'ın başında, <c>MapControllers</c>'tan önce çağrılmalıdır.
    /// </summary>
    /// <param name="app">Uygulama pipeline'ı.</param>
    /// <returns>Zincirleme için aynı <paramref name="app"/>.</returns>
    public static IApplicationBuilder UseBaseForge(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // En başta: sonraki tüm middleware'lerin (exception handling, request logging) ve
        // handler'ların logları doğru CorrelationId'yi taşısın diye.
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        var features = app.ApplicationServices.GetService<BaseForgeFeatures>();
        if (features?.JwtEnabled == true)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        return app;
    }
}
