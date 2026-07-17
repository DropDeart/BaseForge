using BaseForge.Core.Logging;

namespace BaseForge.Infrastructure.Logging;

/// <summary>
/// <see cref="ICorrelationIdAccessor"/>'ın <see cref="AsyncLocal{T}"/> tabanlı implementasyonu.
/// Singleton olarak kaydedilir; değer async çağrı zinciri (HTTP request → handler → outbox yazımı,
/// gRPC çağrısı, consumer'ın MediatR dispatch'i) boyunca doğru şekilde akar, farklı eşzamanlı
/// akışlar arasında sızmaz.
/// </summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Storage = new();

    /// <inheritdoc />
    public string? Current
    {
        get => Storage.Value;
        set => Storage.Value = value;
    }
}
