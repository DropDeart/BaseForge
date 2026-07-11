using MediatR;

namespace BaseForge.Core.Messaging;

/// <summary>
/// Servisler arası asenkron (RabbitMQ) yayınlanan bir olayı işaretler. MediatR <see cref="INotification"/>
/// üzerine kuruludur: yayıncı <see cref="IEventBus"/> ile kuyruğa yazar, tüketici tarafında kuyruktan
/// gelen mesaj yerel olarak <c>IPublisher.Publish</c> ile dağıtılır — geliştirici sıradan bir
/// <see cref="INotificationHandler{TNotification}"/> yazar, RabbitMQ'yu hiç görmez.
/// </summary>
public interface IIntegrationEvent : INotification
{
    /// <summary>Olayın benzersiz kimliği (idempotency/izleme için).</summary>
    Guid EventId { get; }

    /// <summary>Olayın oluşturulma zamanı (UTC).</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Olay tipi, <c>servis/EntityKind</c> biçiminde (örn. <c>blog/CommentCreated</c>).
    /// RabbitMQ routing key'i buradan türetilir (<c>/</c> → <c>.</c>).
    /// </summary>
    string EventType { get; }
}
