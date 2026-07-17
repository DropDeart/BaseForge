using BaseForge.Core.Logging;
using Serilog.Context;

namespace BaseForge.Infrastructure.Logging;

/// <summary>
/// <see cref="ICorrelationIdAccessor"/> için ortak "sınır girişi" mantığı — HTTP middleware, gRPC
/// server interceptor'ı ve RabbitMQ consumer'ı hepsi aynı üç adımı yapar: gelen id'yi kullan (yoksa
/// üret), accessor'a yaz, Serilog <see cref="LogContext"/>'e ekle. Bu üç çağıran, birbirinden
/// bağımsız olarak aynı mantığı tekrar yazmak yerine burayı paylaşır.
/// </summary>
public static class CorrelationIdAccessorExtensions
{
    /// <summary>
    /// <paramref name="incomingId"/> doluysa onu, boşsa yeni üretilen bir id'yi
    /// <paramref name="accessor"/>'a yazar ve Serilog <see cref="LogContext"/>'e ekler. Dönen
    /// <see cref="IDisposable"/>, <c>using</c> ile kapsam sonunda LogContext girdisini kaldırır.
    /// </summary>
    /// <param name="accessor">O anki akışın correlation id'sinin yazılacağı accessor.</param>
    /// <param name="incomingId">Üst taraftan gelen id (HTTP header, gRPC metadata, event zarfı) — boş/null olabilir.</param>
    public static IDisposable EnterScope(this ICorrelationIdAccessor accessor, string? incomingId)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        var correlationId = string.IsNullOrWhiteSpace(incomingId) ? Guid.NewGuid().ToString() : incomingId;
        accessor.Current = correlationId;
        return LogContext.PushProperty("CorrelationId", correlationId);
    }
}
