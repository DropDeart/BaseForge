using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>Ham (zaten zarflanmış) bir olayı RabbitMQ'ya yazan alt seviye publisher.</summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    /// <paramref name="envelopeJson"/>'u (bir <see cref="EventEnvelope"/>'un serileştirilmiş hâli)
    /// olduğu gibi RabbitMQ'ya yazar — hiçbir yeniden serileştirme/deserileştirme yapmaz.
    /// </summary>
    /// <param name="eventType">Olay tipi (<c>servis/EntityKind</c>) — routing key buradan türetilir.</param>
    /// <param name="envelopeJson">Mesaj body'si olarak gönderilecek, zaten hazır zarf JSON'u.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task PublishRawAsync(string eventType, string envelopeJson, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IRabbitMqPublisher"/>'ın RabbitMQ (topic exchange) implementasyonu. Şu an tek çağıran
/// <c>OutboxPublisherHostedService</c>'tir — uygulama kodu event yayınlamak için <c>IEventBus</c>
/// (<c>OutboxEventBus</c>) kullanır, bu sınıfı hiç görmez.
/// </summary>
public sealed class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private volatile bool _exchangeDeclared;

    /// <summary>Verilen bağlantı yöneticisi ve ayarlarla bir publisher oluşturur.</summary>
    /// <param name="connectionManager">Paylaşılan RabbitMQ bağlantısı.</param>
    /// <param name="options">Exchange/broker ayarları.</param>
    public RabbitMqPublisher(RabbitMqConnectionManager connectionManager, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);
        _connectionManager = connectionManager;
        _options = options;
    }

    /// <inheritdoc />
    public async Task PublishRawAsync(string eventType, string envelopeJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeJson);

        var channel = await _connectionManager.RentChannelAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureExchangeDeclaredAsync(channel, cancellationToken).ConfigureAwait(false);

            var body = Encoding.UTF8.GetBytes(envelopeJson);
            var routingKey = eventType.Replace('/', '.');

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
        finally
        {
            await _connectionManager.ReturnChannelAsync(channel).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Exchange'i yalnızca bir kere declare eder (bu publisher Singleton olduğu için uygulama
    /// ömrü boyunca bir kere yeterli) — her <see cref="PublishRawAsync"/> çağrısında tekrar declare
    /// etmek, declare zaten idempotent olsa da gereksiz bir AMQP round-trip'idir. Bağlantı
    /// koptuğunda RabbitMQ.Client'ın topology recovery'si (<c>TopologyRecoveryEnabled</c>, bkz.
    /// <see cref="RabbitMqConnectionManager"/>) declare edilen exchange'i otomatik olarak yeniden
    /// kurar, bu yüzden burada manuel bir yeniden-declare mantığına gerek yoktur.
    /// Kilitsiz, "best effort" bir kontrol: eşzamanlı ilk çağrılar teorik olarak exchange'i birden
    /// fazla kez declare edebilir (declare idempotent olduğu için zararsız) — bir kilit nesnesi
    /// eklemenin (ve onu <see cref="IDisposable"/> yapmanın) getirisi olmayan bir karmaşıklık olurdu.
    /// </summary>
    private async Task EnsureExchangeDeclaredAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_exchangeDeclared)
        {
            return;
        }

        await channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _exchangeDeclared = true;
    }
}

/// <summary>Kuyruğa yazılan zarf: olay meta verisi + gerçek payload (<see cref="Data"/>).</summary>
/// <param name="EventId">Olay kimliği.</param>
/// <param name="OccurredAt">Oluşma zamanı.</param>
/// <param name="EventType">Olay tipi (<c>servis/EntityKind</c>).</param>
/// <param name="Data">Olayın gerçek alanları (yayıncı tipin JSON serileştirmesi).</param>
/// <param name="CorrelationId">
/// Olayı tetikleyen isteğin correlation id'si (varsa) — tüketici tarafında
/// <c>RabbitMqConsumerHostedService</c> bunu geri yükleyerek handler loglarının orijinal
/// istekle aynı id'yi taşımasını sağlar.
/// </param>
internal sealed record EventEnvelope(Guid EventId, DateTimeOffset OccurredAt, string EventType, JsonElement Data, string? CorrelationId = null);
