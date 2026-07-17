using System.Text.Json;
using BaseForge.Core.Logging;
using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Data;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEventBus"/>'ın transactional outbox implementasyonu. RabbitMQ'ya hiç dokunmaz —
/// olayı bir <see cref="OutboxMessage"/> satırı olarak çağıranın o anki
/// <see cref="BaseForgeDbContext"/>'inin change tracker'ına ekler. Aynı scope'taki
/// <c>IUnitOfWork.SaveChangesAsync</c> çağrısı bu satırı, tetikleyen business entity
/// değişikliğiyle AYNI transaction'da commit eder. Gerçek publish, ayrı bir arka plan relay'i
/// (<c>OutboxPublisherHostedService</c>) tarafından yapılır — bu yüzden <see cref="IEventBus"/>
/// (Singleton yerine) <b>Scoped</b> kaydedilmelidir: aksi halde farklı bir scope'un
/// <see cref="BaseForgeDbContext"/>'ine yazardı.
/// </summary>
public sealed class OutboxEventBus : IEventBus
{
    private readonly BaseForgeDbContext _context;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    /// <summary>Çağıranla aynı scope'taki DbContext ile bir outbox event bus oluşturur.</summary>
    /// <param name="context">Bu isteğin/scope'un DbContext'i (repository'ler ve UnitOfWork ile paylaşılan aynı instance).</param>
    /// <param name="correlationIdAccessor">O anki isteğin/akışın correlation id'sine erişim.</param>
    public OutboxEventBus(BaseForgeDbContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);
        _context = context;
        _correlationIdAccessor = correlationIdAccessor;
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var envelope = new EventEnvelope(
            integrationEvent.EventId,
            integrationEvent.OccurredAt,
            integrationEvent.EventType,
            JsonSerializer.SerializeToElement(integrationEvent, integrationEvent.GetType()),
            _correlationIdAccessor.Current);

        var outboxMessage = new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            OccurredAt = integrationEvent.OccurredAt,
            Payload = JsonSerializer.Serialize(envelope),
        };

        _context.OutboxMessages.Add(outboxMessage);
        return Task.CompletedTask;
    }
}
