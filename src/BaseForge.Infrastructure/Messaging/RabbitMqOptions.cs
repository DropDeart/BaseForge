using BaseForge.Core.Messaging;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ bağlantı ve abonelik ayarları. <c>BaseForge.API</c>'deki
/// <c>BaseForgeOptions.EnableRabbitMq</c> çağrısıyla doldurulur.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Broker host adresi.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Broker AMQP portu.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Bağlantı kullanıcı adı.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>Bağlantı parolası.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Tüm servislerin ortak kullandığı topic exchange adı. Routing key'ler
    /// <c>IIntegrationEvent.EventType</c>'ten türetilir (<c>servis/EntityKind</c> → <c>servis.EntityKind</c>).
    /// </summary>
    public string ExchangeName { get; set; } = "baseforge.events";

    /// <summary>Bu servisin dinlediği olay abonelikleri (<see cref="Subscribe{TEvent}"/> ile eklenir).</summary>
    public IReadOnlyList<EventSubscription> Subscriptions => _subscriptions;

    /// <summary>
    /// <c>OutboxPublisherHostedService</c>'in outbox tablosunu ne sıklıkla taradığı (varsayılan 2 sn).
    /// </summary>
    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Bir taramada en fazla işlenecek outbox satırı sayısı (varsayılan 50).</summary>
    public int OutboxBatchSize { get; set; } = 50;

    /// <summary>
    /// Bir outbox satırı bu kadar başarısız denemeden sonra <c>IsDead</c> olarak işaretlenir ve
    /// artık taranmaz (varsayılan 10). Sınırsız retry yerine — sürekli başarısız olan bir mesajın
    /// relay'i sonsuza kadar meşgul etmesini önler.
    /// </summary>
    public int OutboxMaxRetries { get; set; } = 10;

    /// <summary>
    /// Başarıyla işlenmiş (<c>ProcessedAt</c> dolu) outbox satırlarının ne kadar süre sonra
    /// silineceği (varsayılan 7 gün). <c>IsDead</c> satırlar bu temizlikten muaftır (manuel
    /// inceleme için kalır).
    /// </summary>
    public TimeSpan OutboxRetention { get; set; } = TimeSpan.FromDays(7);

    private readonly List<EventSubscription> _subscriptions = [];

    /// <summary>
    /// Bir olay tipine abone olur: <paramref name="queueName"/> adında dayanıklı (durable) bir kuyruk
    /// oluşturulup <paramref name="eventType"/>'tan türeyen routing key'e bağlanır. Mesaj geldiğinde
    /// <typeparamref name="TEvent"/>'e deserialize edilip yerel olarak MediatR ile dağıtılır.
    /// </summary>
    /// <typeparam name="TEvent">Tüketilecek olay tipi (yayıncı taraftaki gerçek CLR tipiyle alan uyumlu olmalı).</typeparam>
    /// <param name="eventType">Olay tipi, <c>servis/EntityKind</c> biçiminde (örn. <c>blog/CommentCreated</c>).</param>
    /// <param name="queueName">Bu servise özel kuyruk adı (örn. <c>blog.NotifyPostAuthorOnComment</c>).</param>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public RabbitMqOptions Subscribe<TEvent>(string eventType, string queueName)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _subscriptions.Add(new EventSubscription(typeof(TEvent), eventType, queueName));
        return this;
    }
}

/// <summary>Tek bir olay aboneliğinin çözümlenmiş hâli (kuyruk adı + routing key + hedef CLR tipi).</summary>
/// <param name="EventClrType">Mesajın deserialize edileceği CLR tipi.</param>
/// <param name="EventType">Olay tipi (<c>servis/EntityKind</c>).</param>
/// <param name="QueueName">Dayanıklı kuyruk adı.</param>
public sealed record EventSubscription(Type EventClrType, string EventType, string QueueName)
{
    /// <summary>RabbitMQ routing key'i (<c>EventType</c>'teki <c>/</c> → <c>.</c>).</summary>
    public string RoutingKey => EventType.Replace('/', '.');
}
