namespace BaseForge.Core.Messaging;

/// <summary>
/// Tüketici tarafı idempotency (Inbox pattern) satırı: bir <see cref="IIntegrationEvent"/>'in
/// (<c>EventId</c>'siyle) bu serviste daha önce başarıyla işlendiğini işaretler. Outbox'ın
/// <b>at-least-once</b> teslim garantisi yüzünden aynı olay iki kez teslim edilebilir (örn. relay
/// <c>ProcessedAt</c>'i commit etmeden önce çökerse); tüketici (<c>RabbitMqConsumerHostedService</c>,
/// <c>BaseForge.Infrastructure</c>) bir olayı işlemeden önce bu tabloda arar, bulursa handler'ı
/// tekrar çalıştırmadan atlar. Bilinçli olarak <c>IAuditEntity</c>/<c>ISoftDelete</c>/
/// <c>ITenantEntity</c> implemente ETMEZ — <c>OutboxMessage</c> ile aynı gerekçeyle, soft-delete/tenant
/// global query filter'larına girmemeli.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>İşlenen olayın kimliği (<see cref="IIntegrationEvent.EventId"/>) — birincil anahtar.</summary>
    public Guid Id { get; set; }

    /// <summary>Handler'ın başarıyla tamamlandığı an (UTC).</summary>
    public DateTimeOffset ProcessedAt { get; set; }
}
