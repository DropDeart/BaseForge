using System.Text;
using System.Text.Json;
using BaseForge.Core.Logging;
using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// <see cref="RabbitMqOptions.Subscriptions"/>'ta kayıtlı her olay için dayanıklı bir kuyruk açıp
/// dinler; gelen mesajı ilgili CLR tipine deserialize edip yerel olarak MediatR (<see cref="IPublisher"/>)
/// ile dağıtır. Yalnızca en az bir abonelik varsa (<c>AddBaseForge</c> tarafından) DI'a eklenir.
/// Dağıtmadan önce <see cref="InboxMessage"/> tablosuna bakarak aynı <c>EventId</c>'nin bu serviste
/// daha önce işlenip işlenmediğini kontrol eder (idempotency — outbox'un at-least-once teslimine karşı).
/// Kalıcı olarak başarısız olan mesajlar (<c>nack</c> + <c>requeue: false</c>) artık sessizce
/// silinmez — her abonelik için ayrı bir dead-letter kuyruğuna (<c>{queue}.dead</c>) yönlenir.
/// </summary>
public sealed class RabbitMqConsumerHostedService : BackgroundService
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerHostedService> _logger;
    private IChannel? _channel;

    /// <summary>Verilen bağımlılıklarla bir tüketici hosted service'i oluşturur.</summary>
    public RabbitMqConsumerHostedService(
        RabbitMqConnectionManager connectionManager,
        RabbitMqOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionManager = connectionManager;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connectionManager.CreateChannelAsync(stoppingToken).ConfigureAwait(false);

        await _channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);

        // Dead-letter exchange: reddedilen (nack + requeue:false) mesajlar buraya yönlenir.
        // Bu olmadan RabbitMQ, hedefsiz bir mesajı sessizce ve KALICI olarak siler — asıl veri
        // kaybı burada oluyordu; DLX bunu her abonelik için bir "{queue}.dead" kuyruğuna çevirir.
        var deadLetterExchange = _options.ExchangeName + ".dlx";
        await _channel.ExchangeDeclareAsync(
            deadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken).ConfigureAwait(false);

        foreach (var subscription in _options.Subscriptions)
        {
            var deadQueueName = subscription.QueueName + ".dead";
            await _channel.QueueDeclareAsync(
                deadQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken).ConfigureAwait(false);

            await _channel.QueueBindAsync(
                deadQueueName,
                deadLetterExchange,
                routingKey: string.Empty,
                cancellationToken: stoppingToken).ConfigureAwait(false);

            await _channel.QueueDeclareAsync(
                subscription.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = deadLetterExchange },
                cancellationToken: stoppingToken).ConfigureAwait(false);

            await _channel.QueueBindAsync(
                subscription.QueueName,
                _options.ExchangeName,
                subscription.RoutingKey,
                cancellationToken: stoppingToken).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(subscription, delivery, stoppingToken);

            await _channel.BasicConsumeAsync(
                subscription.QueueName,
                autoAck: false,
                consumer,
                cancellationToken: stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task HandleDeliveryAsync(EventSubscription subscription, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(delivery.Body.Span);
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(json)
                ?? throw new InvalidOperationException("Olay zarfı deserialize edilemedi.");

            await using var scope = _scopeFactory.CreateAsyncScope();

            // Olayı yayınlayan isteğin correlation id'sini geri yükle — bu event'i işleyen
            // handler'ın logları, orijinal HTTP/gRPC isteğiyle aynı id'yi taşısın.
            using var correlationScope = scope.ServiceProvider.GetRequiredService<ICorrelationIdAccessor>().EnterScope(envelope.CorrelationId);

            var context = scope.ServiceProvider.GetRequiredService<BaseForgeDbContext>();

            // Inbox pattern (idempotency): outbox'un at-least-once teslimi yüzünden aynı EventId iki
            // kez gelebilir (örn. relay ProcessedAt'i commit etmeden önce çökerse). Daha önce işlendiyse
            // handler'ı tekrar çalıştırmadan atla.
            if (await context.InboxMessages.AnyAsync(m => m.Id == envelope.EventId, stoppingToken).ConfigureAwait(false))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Olay zaten işlenmiş (muhtemelen at-least-once tekrar teslimi), atlanıyor: {EventType} ({EventId}).",
                        envelope.EventType, envelope.EventId);
                }

                await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
                return;
            }

            var notification = envelope.Data.Deserialize(subscription.EventClrType)
                ?? throw new InvalidOperationException($"'{subscription.EventType}' olayı '{subscription.EventClrType}' tipine deserialize edilemedi.");

            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(notification, stoppingToken).ConfigureAwait(false);

            // Mark-AFTER, mark-before değil: işaretleme handler'dan sonra yapılır. Handler çökerse
            // (ack hiç gitmez, mesaj yeniden teslim edilir) Inbox satırı henüz commit edilmediği için
            // bir sonraki teslimat hâlâ "işlenmemiş" görür ve tekrar dener — mark-before olsaydı event
            // yanlışlıkla "zaten işlendi" sayılıp tamamen kaybolurdu.
            context.InboxMessages.Add(new InboxMessage { Id = envelope.EventId, ProcessedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

            await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Mesaj requeue edilmeden reddedilir ("fire and forget" felsefesiyle tutarlı) — ama
            // artık kaybolmaz: kuyruğun x-dead-letter-exchange argümanı sayesinde '{queue}.dead'e
            // yönlenir (bkz. ExecuteAsync). Otomatik gecikmeli retry (N kere yeniden dene, sonra
            // DLQ) v1 kapsamında değil — bkz. docs/ARCH.md §5.2.
            _logger.LogError(ex, "'{QueueName}' kuyruğundan gelen mesaj işlenemedi, '{DeadQueueName}'e yönlendirilecek.",
                subscription.QueueName, subscription.QueueName + ".dead");
            await _channel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
