namespace BaseForge.Core.Messaging;

/// <summary>
/// Servisler arası asenkron olay yayınlama sözleşmesi (RabbitMQ üzerinden). Somut implementasyon
/// <c>BaseForge.Infrastructure</c>'dadır (<c>OutboxEventBus</c>): olay, çağıranın o anki
/// <c>DbContext</c>'inin change tracker'ına bir transactional outbox satırı olarak eklenir — gerçek
/// RabbitMQ publish'i ayrı bir arka plan relay'i (<c>OutboxPublisherHostedService</c>) tarafından,
/// business entity değişikliğiyle aynı transaction commit olduktan sonra güvenilir şekilde yapılır.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Bir olayı yayınlanmak üzere kuyruğa alır (outbox'a ekler). Çağıranın o anki
    /// <c>SaveChangesAsync</c> çağrısı commit edilmeden gerçek broker'a gitmez; teslim garantisi/retry
    /// politikası relay implementasyonuna bağlıdır.
    /// </summary>
    /// <typeparam name="TEvent">Yayınlanan olay tipi.</typeparam>
    /// <param name="integrationEvent">Yayınlanacak olay.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
