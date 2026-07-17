namespace BaseForge.Core.Logging;

/// <summary>
/// O anki mantıksal işlemin (HTTP isteği, gRPC çağrısı veya tüketilen RabbitMQ event'i)
/// correlation id'sine ambient erişim sağlar. Somut implementasyon <c>BaseForge.Infrastructure</c>'dadır
/// (<c>CorrelationIdAccessor</c>, <see cref="System.Threading.AsyncLocal{T}"/> tabanlı) — HTTP middleware,
/// gRPC interceptor'ları ve RabbitMQ consumer/outbox event bus bu id'yi burdan okuyup yazar, böylece
/// bir isteğin HTTP → gRPC → RabbitMQ event zinciri boyunca aynı id ile loglanması sağlanır.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>O anki akışın correlation id'si; henüz atanmadıysa <see langword="null"/>.</summary>
    string? Current { get; set; }
}
