using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BaseForge.IntegrationTests.Messaging;

/// <summary>GEN-8: broker yokken tüketici host'u düşürmez, yeniden dener ve düzgün durur (Docker gerekmez).</summary>
public sealed class ConsumerResilienceTests
{
    [Fact]
    public async Task UnreachableBroker_DoesNotFault_AndStopsCleanly()
    {
        var options = new RabbitMqOptions { Host = "127.0.0.1", Port = 1, Username = "guest", Password = "guest" };
        options.Subscribe<Ping>("resilience/Ping", "resilience.queue");

        await using var connectionManager = new RabbitMqConnectionManager(options);
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var consumer = new RabbitMqConsumerHostedService(connectionManager, options, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RabbitMqConsumerHostedService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));   // ilk deneme + bir yeniden deneme

        Assert.NotNull(consumer.ExecuteTask);
        Assert.False(consumer.ExecuteTask.IsFaulted);   // önceden BrokerUnreachableException ile host duruyordu

        await consumer.StopAsync(CancellationToken.None);
    }

    private sealed class Ping : IIntegrationEvent
    {
        public Guid EventId { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public string EventType { get; set; } = string.Empty;
    }
}
