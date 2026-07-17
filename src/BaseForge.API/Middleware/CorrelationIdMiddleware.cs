using BaseForge.Core.Logging;
using BaseForge.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;

namespace BaseForge.API.Middleware;

/// <summary>
/// Pipeline'ın en başına eklenir (<see cref="Extensions.ApplicationBuilderExtensions.UseBaseForge"/>).
/// Gelen <c>X-Correlation-Id</c> header'ını kullanır (yoksa yeni bir tane üretir) ve
/// <c>ICorrelationIdAccessor.EnterScope</c> (<c>BaseForge.Infrastructure</c>) ile hem accessor'a
/// yazar hem Serilog log context'ine ekler — bu sayede pipeline'daki ve handler'daki tüm loglar
/// aynı correlation id'yi taşır. Response'a da aynı header'ı ekler, böylece çağıran taraf
/// downstream'de üretilen id'yi görebilir.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    /// <summary>Yeni bir middleware örneği oluşturur.</summary>
    /// <param name="next">Pipeline'daki bir sonraki bileşen.</param>
    /// <param name="correlationIdAccessor">O anki isteğin correlation id'sinin yazılacağı accessor.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);
        _next = next;
        _correlationIdAccessor = correlationIdAccessor;
    }

    /// <summary>İsteği correlation id ile etiketleyip pipeline'ın geri kalanını çalıştırır.</summary>
    /// <param name="context">HTTP bağlamı.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incomingId = context.Request.Headers.TryGetValue(HeaderName, out var existing) ? existing.ToString() : null;

        using (_correlationIdAccessor.EnterScope(incomingId))
        {
            context.Response.Headers[HeaderName] = _correlationIdAccessor.Current!;
            await _next(context).ConfigureAwait(false);
        }
    }
}
