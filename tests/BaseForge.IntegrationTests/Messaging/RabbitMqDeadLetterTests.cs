using System.Text;
using System.Text.Json;
using BaseForge.Core.Logging;
using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Logging;
using BaseForge.Infrastructure.Messaging;
using BaseForge.IntegrationTests.Persistence;
using BaseForge.IntegrationTests.Support;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaseForge.IntegrationTests.Messaging;

/// <summary>
/// <see cref="RabbitMqConsumerHostedService"/>'in dead-letter yönlendirmesini GERÇEK RabbitMQ'ya
/// karşı doğrular: bir mesaj handler'da sürekli başarısız olursa, önceki davranışın aksine
/// (sessizce kalıcı silinme) artık <c>{queue}.dead</c> kuyruğunda görünür kalır.
/// </summary>
public sealed class RabbitMqDeadLetterTests : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private readonly PostgresFixture _postgres;
    private readonly RabbitMqFixture _rabbit;

    public RabbitMqDeadLetterTests(PostgresFixture postgres, RabbitMqFixture rabbit)
    {
        _postgres = postgres;
        _rabbit = rabbit;
    }

    [DockerFact]
    public async Task FailingMessage_IsRoutedToDeadLetterQueue_InsteadOfBeingDropped()
    {
        var connectionString = TestDbContextFactory.UniqueConnectionString(_postgres.ConnectionString, "dlq");
        var eventType = $"dlqtest/{Guid.NewGuid():N}";
        var queueName = $"dlqtest.queue.{Guid.NewGuid():N}";
        var deadQueueName = queueName + ".dead";
        var eventId = Guid.NewGuid();

        var options = new RabbitMqOptions
        {
            Host = _rabbit.Host,
            Port = _rabbit.Port,
            Username = _rabbit.Username,
            Password = _rabbit.Password,
        };
        options.Subscribe<FailingNotification>(eventType, queueName);

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<BaseForgeDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FailingNotificationHandler>());
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        await using var connectionManager = new RabbitMqConnectionManager(options);
        var rawPublisher = new RabbitMqPublisher(connectionManager, options);

        var consumer = new RabbitMqConsumerHostedService(
            connectionManager,
            options,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RabbitMqConsumerHostedService>.Instance);

        await consumer.StartAsync(CancellationToken.None);

        var envelopeJson = BuildEnvelopeJson(eventId, eventType);

        // Kuyruk/DLX kurulumu arka planda tamamlanana kadar birkaç kez dene (bkz. idempotency
        // testindeki aynı gerekçe).
        for (var i = 0; i < 10; i++)
        {
            await rawPublisher.PublishRawAsync(eventType, envelopeJson);
            await Task.Delay(300);
        }

        var foundInDeadQueue = await WaitUntilDeadLetterAsync(connectionManager, deadQueueName, eventId, TimeSpan.FromSeconds(10));
        await consumer.StopAsync(CancellationToken.None);

        Assert.True(foundInDeadQueue, "Sürekli başarısız olan mesaj dead-letter kuyruğuna yönlendirilmedi.");
    }

    private static async Task<bool> WaitUntilDeadLetterAsync(RabbitMqConnectionManager connectionManager, string deadQueueName, Guid eventId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var channel = await connectionManager.CreateChannelAsync();
                await using (channel.ConfigureAwait(false))
                {
                    var result = await channel.BasicGetAsync(deadQueueName, autoAck: true);
                    if (result is not null)
                    {
                        var json = Encoding.UTF8.GetString(result.Body.Span);
                        if (json.Contains(eventId.ToString(), StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Kuyruk henüz oluşmamış olabilir (consumer'ın arka plan kurulumu sürüyor) — tekrar dene.
            }

            await Task.Delay(200);
        }

        return false;
    }

    private static string BuildEnvelopeJson(Guid eventId, string eventType)
    {
        var data = JsonSerializer.SerializeToElement(new FailingNotification
        {
            EventId = eventId,
            OccurredAt = DateTimeOffset.UtcNow,
            EventType = eventType,
        });

        var envelope = new EventEnvelope(eventId, DateTimeOffset.UtcNow, eventType, data, null);
        return JsonSerializer.Serialize(envelope);
    }

    private sealed class FailingNotification : IIntegrationEvent
    {
        public Guid EventId { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public string EventType { get; set; } = string.Empty;
    }

    private sealed class FailingNotificationHandler : INotificationHandler<FailingNotification>
    {
        public Task Handle(FailingNotification notification, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Simulated handler failure.");
    }
}
