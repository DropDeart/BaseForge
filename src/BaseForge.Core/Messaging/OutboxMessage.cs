namespace BaseForge.Core.Messaging;

/// <summary>
/// Transactional Outbox satırı: bir <see cref="IIntegrationEvent"/>'in, yayınlandığı business
/// entity değişikliğiyle AYNI <c>SaveChangesAsync</c> çağrısında (aynı transaction'da) yazılmış
/// hâli. Gerçek RabbitMQ publish'i buradan ayrı, arka planda çalışan bir relay
/// (<c>OutboxPublisherHostedService</c>, <c>BaseForge.Infrastructure</c>) tarafından yapılır.
/// Bilinçli olarak <c>IAuditEntity</c>/<c>ISoftDelete</c>/<c>ITenantEntity</c>
/// implemente ETMEZ: soft-delete/tenant global query filter'larına girmemeli, işlenen satırlar
/// fiziksel olarak silinebilmeli.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Yayınlanan olayın kimliği (<see cref="IIntegrationEvent.EventId"/>) — birincil anahtar.
    /// Satır zaten bir olayla 1:1 olduğu için ayrı bir yapay birincil anahtar tutulmaz
    /// (<c>InboxMessage</c> ile aynı desen).
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>Olay tipi, <c>servis/EntityKind</c> biçiminde.</summary>
    public required string EventType { get; set; }

    /// <summary>Olayın oluşturulma zamanı (UTC) — relay bu sırayla işler.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// RabbitMQ'ya gönderilecek tam zarf (<c>EventEnvelope</c>) JSON'u — relay bunu hiç
    /// yeniden oluşturmadan doğrudan mesaj body'si olarak kullanır.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>Relay tarafından başarıyla RabbitMQ'ya publish edildiği an; henüz işlenmediyse <see langword="null"/>.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Son başarısız publish denemesinin hata mesajı (varsa).</summary>
    public string? Error { get; set; }

    /// <summary>Başarısız publish deneme sayısı.</summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// <see cref="RetryCount"/>, <c>RabbitMqOptions.OutboxMaxRetries</c>'i (<c>BaseForge.Infrastructure</c>)
    /// aştığında relay tarafından işaretlenir — bu satır artık taranmaz/tekrar denenmez. Manuel
    /// inceleme için satır silinmez, yalnızca dışlanır.
    /// </summary>
    public bool IsDead { get; set; }
}
