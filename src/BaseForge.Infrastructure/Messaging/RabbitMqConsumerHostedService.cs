using System.Text;
using System.Text.Json;
using MediatR;
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

        foreach (var subscription in _options.Subscriptions)
        {
            await _channel.QueueDeclareAsync(
                subscription.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
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

            var notification = envelope.Data.Deserialize(subscription.EventClrType)
                ?? throw new InvalidOperationException($"'{subscription.EventType}' olayı '{subscription.EventClrType}' tipine deserialize edilemedi.");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(notification, stoppingToken).ConfigureAwait(false);

            await _channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // v1: DLQ/retry politikası yok — hata loglanır, mesaj requeue edilmeden reddedilir
            // ("fire and forget" felsefesiyle tutarlı, bkz. docs/ARCH.md §5.2).
            _logger.LogError(ex, "'{QueueName}' kuyruğundan gelen mesaj işlenemedi.", subscription.QueueName);
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
