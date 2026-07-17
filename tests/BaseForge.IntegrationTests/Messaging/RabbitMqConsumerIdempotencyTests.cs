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
/// <see cref="RabbitMqConsumerHostedService"/>'in Inbox pattern (idempotency) davranışını GERÇEK
/// RabbitMQ + PostgreSQL'e karşı doğrular: aynı <c>EventId</c>'ye sahip bir olay birden çok kez
/// teslim edilse bile (outbox'ın at-least-once garantisini simüle eder), MediatR handler'ı yalnızca
/// BİR kez çalışır ve <c>InboxMessages</c> tablosunda tek bir satır oluşur.
/// </summary>
public sealed class RabbitMqConsumerIdempotencyTests : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private readonly PostgresFixture _postgres;
    private readonly RabbitMqFixture _rabbit;

    public RabbitMqConsumerIdempotencyTests(PostgresFixture postgres, RabbitMqFixture rabbit)
    {
        _postgres = postgres;
        _rabbit = rabbit;
    }

    [DockerFact]
    public async Task DuplicateDelivery_OfSameEventId_OnlyInvokesHandlerOnce()
    {
        var connectionString = TestDbContextFactory.UniqueConnectionString(_postgres.ConnectionString, "inbox");
        var eventType = $"inboxtest/{Guid.NewGuid():N}";
        var queueName = $"inboxtest.queue.{Guid.NewGuid():N}";
        var eventId = Guid.NewGuid();

        var options = new RabbitMqOptions
        {
            Host = _rabbit.Host,
            Port = _rabbit.Port,
            Username = _rabbit.Username,
            Password = _rabbit.Password,
        };
        options.Subscribe<TestNotification>(eventType, queueName);

        var counter = new HandlerCallCounter();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<BaseForgeDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton(counter);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<TestNotificationHandler>());
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

        // Aynı EventId'yi birkaç kez, aralıklarla yayınla — consumer'ın kuyruk/binding kurulumu
        // arka planda asenkron tamamlandığı için ilk bir-iki yayın kaybolabilir (henüz kuyruk yok);
        // sonrakiler kesin ulaşır ve gerçek bir "at-least-once tekrar teslimi" simülasyonu oluşturur.
        for (var i = 0; i < 5; i++)
        {
            await rawPublisher.PublishRawAsync(eventType, envelopeJson);
            await Task.Delay(200);
        }

        await PollingHelpers.WaitUntilAsync(() => counter.Count >= 1, TimeSpan.FromSeconds(15));
        // Kalan (duplicate) teslimatların da işlenip idempotency ile atlanması için zaman tanı.
        await Task.Delay(1000);
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(1, counter.Count);

        await using var verify = TestDbContextFactory.Create(connectionString);
        Assert.Equal(1, await verify.InboxMessages.CountAsync(m => m.Id == eventId));
    }

    private static string BuildEnvelopeJson(Guid eventId, string eventType)
    {
        var data = JsonSerializer.SerializeToElement(new TestNotification
        {
            EventId = eventId,
            OccurredAt = DateTimeOffset.UtcNow,
            EventType = eventType,
            Message = "hello",
        });

        var envelope = new EventEnvelope(eventId, DateTimeOffset.UtcNow, eventType, data, null);
        return JsonSerializer.Serialize(envelope);
    }

    private sealed class TestNotification : IIntegrationEvent
    {
        public Guid EventId { get; set; }

        public DateTimeOffset OccurredAt { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    private sealed class TestNotificationHandler(HandlerCallCounter counter) : INotificationHandler<TestNotification>
    {
        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }

    private sealed class HandlerCallCounter
    {
        private int _count;

        public int Count => _count;

        public void Increment() => Interlocked.Increment(ref _count);
    }
}
