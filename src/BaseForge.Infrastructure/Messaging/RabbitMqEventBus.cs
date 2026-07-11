using System.Text;
using System.Text.Json;
using BaseForge.Core.Messaging;
using RabbitMQ.Client;

namespace BaseForge.Infrastructure.Messaging;

/// <summary><see cref="IEventBus"/>'ın RabbitMQ (topic exchange) implementasyonu.</summary>
public sealed class RabbitMqEventBus : IEventBus
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;

    /// <summary>Verilen bağlantı yöneticisi ve ayarlarla bir event bus oluşturur.</summary>
    /// <param name="connectionManager">Paylaşılan RabbitMQ bağlantısı.</param>
    /// <param name="options">Exchange/broker ayarları.</param>
    public RabbitMqEventBus(RabbitMqConnectionManager connectionManager, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);
        _connectionManager = connectionManager;
        _options = options;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var channel = await _connectionManager.CreateChannelAsync(cancellationToken).ConfigureAwait(false);
        await using (channel.ConfigureAwait(false))
        {
            await channel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var envelope = new EventEnvelope(
                integrationEvent.EventId,
                integrationEvent.OccurredAt,
                integrationEvent.EventType,
                JsonSerializer.SerializeToElement(integrationEvent, integrationEvent.GetType()));

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var routingKey = integrationEvent.EventType.Replace('/', '.');

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
            };

            await channel.BasicPublishAsync(
                _options.ExchangeName,
                routingKey,
                mandatory: false,
                properties,
                body,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Kuyruğa yazılan zarf: olay meta verisi + gerçek payload (<see cref="Data"/>).</summary>
/// <param name="EventId">Olay kimliği.</param>
/// <param name="OccurredAt">Oluşma zamanı.</param>
/// <param name="EventType">Olay tipi (<c>servis/EntityKind</c>).</param>
/// <param name="Data">Olayın gerçek alanları (yayıncı tipin JSON serileştirmesi).</param>
internal sealed record EventEnvelope(Guid EventId, DateTimeOffset OccurredAt, string EventType, JsonElement Data);
