namespace BaseForge.Core.Messaging;

/// <summary>
/// Servisler arası asenkron olay yayınlama sözleşmesi (RabbitMQ üzerinden, fire-and-forget).
/// Somut implementasyon <c>BaseForge.Infrastructure</c>'dadır (<c>RabbitMqEventBus</c>).
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Bir olayı yayınlar. Çağıran taraf için fire-and-forget'tir; teslim garantisi/retry
    /// politikası broker tarafı implementasyona bağlıdır.
    /// </summary>
    /// <typeparam name="TEvent">Yayınlanan olay tipi.</typeparam>
    /// <param name="integrationEvent">Yayınlanacak olay.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
