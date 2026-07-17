using System.Threading;
using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Logging;
using BaseForge.Infrastructure.Messaging;
using BaseForge.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaseForge.IntegrationTests.Persistence;

/// <summary>
/// Transactional outbox'ı GERÇEK PostgreSQL'e karşı doğrular (Testcontainers): business entity +
/// outbox satırının atomikliği, rollback'in ikisini birden geri alması, relay'in (
/// <see cref="OutboxPublisherHostedService"/>) bekleyen satırları publish edip işaretlemesi,
/// satır-seviyesi hata izolasyonu, <c>FOR UPDATE SKIP LOCKED</c>'ın çoklu-instance'ta aynı satırı
/// iki kez işletmediği, max-retry aşılınca dead işaretlemesi ve retention penceresinin eskimiş
/// işlenmiş satırları silmesi. Her test kendi izole veritabanında çalışır (paylaşılan container,
/// benzersiz DB adı) — relay'in <c>WHERE "ProcessedAt" IS NULL</c> sorgusu tüm tabloyu taradığı
/// için, aksi halde bir testin kasıtlı olarak işlenmemiş bıraktığı satır bir başka testin relay'i
/// tarafından da görülüp sonuçları kirletirdi.
/// </summary>
public sealed class OutboxIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public OutboxIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    [DockerFact]
    public async Task PublishAsync_And_SaveChanges_WritesBusinessEntityAndOutboxRow_Atomically()
    {
        var connectionString = UniqueDatabase();
        var widgetName = $"widget-{Guid.NewGuid():N}";
        var eventType = $"outboxtest/{Guid.NewGuid():N}";

        await using (var context = TestDbContextFactory.Create(connectionString))
        {
            var bus = new OutboxEventBus(context, new CorrelationIdAccessor());
            context.Widgets.Add(new Widget { Name = widgetName });
            await bus.PublishAsync(new TestEvent(eventType));
            await context.SaveChangesAsync();
        }

        await using var verify = TestDbContextFactory.Create(connectionString);
        Assert.Contains(await verify.Widgets.ToListAsync(), w => w.Name == widgetName);
        Assert.Contains(await verify.OutboxMessages.ToListAsync(), m => m.EventType == eventType);
    }

    [DockerFact]
    public async Task Rollback_DiscardsBothBusinessEntityAndOutboxRow()
    {
        var connectionString = UniqueDatabase();
        var widgetName = $"widget-{Guid.NewGuid():N}";
        var eventType = $"outboxtest/{Guid.NewGuid():N}";

        await using (var context = TestDbContextFactory.Create(connectionString))
        {
            var unitOfWork = new UnitOfWork(context);
            var bus = new OutboxEventBus(context, new CorrelationIdAccessor());

            await unitOfWork.BeginTransactionAsync();
            context.Widgets.Add(new Widget { Name = widgetName });
            await bus.PublishAsync(new TestEvent(eventType));
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.RollbackAsync();
        }

        await using var verify = TestDbContextFactory.Create(connectionString);
        Assert.DoesNotContain(await verify.Widgets.ToListAsync(), w => w.Name == widgetName);
        Assert.DoesNotContain(await verify.OutboxMessages.ToListAsync(), m => m.EventType == eventType);
    }

    [DockerFact]
    public async Task Relay_PublishesPendingMessage_AndMarksProcessed()
    {
        var connectionString = UniqueDatabase();
        var eventType = $"outboxtest/{Guid.NewGuid():N}";

        await using (var seed = TestDbContextFactory.Create(connectionString))
        {
            seed.OutboxMessages.Add(NewMessage(eventType));
            await seed.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher();
        await using var provider = BuildProvider(connectionString, publisher);
        var service = new OutboxPublisherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqOptions { OutboxPollingInterval = TimeSpan.FromMilliseconds(100), OutboxBatchSize = 10 },
            NullLogger<OutboxPublisherHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await PollingHelpers.WaitUntilAsync(() => publisher.PublishedEventTypes.Contains(eventType));
        await service.StopAsync(CancellationToken.None);

        await using var verify = TestDbContextFactory.Create(connectionString);
        var row = Assert.Single(await verify.OutboxMessages.Where(m => m.EventType == eventType).ToListAsync());
        Assert.NotNull(row.ProcessedAt);
    }

    [DockerFact]
    public async Task Relay_PerRowFailure_DoesNotBlockOtherRows_AndKeepsFailedRowForRetry()
    {
        var connectionString = UniqueDatabase();
        var failingEventType = $"outboxtest/fails/{Guid.NewGuid():N}";
        var succeedingEventType = $"outboxtest/ok/{Guid.NewGuid():N}";

        await using (var seed = TestDbContextFactory.Create(connectionString))
        {
            seed.OutboxMessages.Add(NewMessage(failingEventType));
            seed.OutboxMessages.Add(NewMessage(succeedingEventType));
            await seed.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher(failEventType: failingEventType);
        await using var provider = BuildProvider(connectionString, publisher);
        var service = new OutboxPublisherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqOptions { OutboxPollingInterval = TimeSpan.FromMilliseconds(100), OutboxBatchSize = 10 },
            NullLogger<OutboxPublisherHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await PollingHelpers.WaitUntilAsync(() => publisher.PublishedEventTypes.Contains(succeedingEventType));
        // Başarısız satırın en az bir kez tekrar denendiğinden emin olmak için birkaç tick daha bekle.
        await PollingHelpers.WaitUntilAsync(() => publisher.AttemptCount(failingEventType) >= 2);
        await service.StopAsync(CancellationToken.None);

        await using var verify = TestDbContextFactory.Create(connectionString);
        var failed = await verify.OutboxMessages.SingleAsync(m => m.EventType == failingEventType);
        var succeeded = await verify.OutboxMessages.SingleAsync(m => m.EventType == succeedingEventType);

        Assert.Null(failed.ProcessedAt);
        Assert.True(failed.RetryCount >= 1);
        Assert.NotNull(failed.Error);
        Assert.NotNull(succeeded.ProcessedAt);
    }

    [DockerFact]
    public async Task Relay_MarksMessageDead_AfterExceedingMaxRetries()
    {
        var connectionString = UniqueDatabase();
        var failingEventType = $"outboxtest/dead/{Guid.NewGuid():N}";

        await using (var seed = TestDbContextFactory.Create(connectionString))
        {
            seed.OutboxMessages.Add(NewMessage(failingEventType));
            await seed.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher(failEventType: failingEventType);
        await using var provider = BuildProvider(connectionString, publisher);
        var service = new OutboxPublisherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqOptions { OutboxPollingInterval = TimeSpan.FromMilliseconds(50), OutboxBatchSize = 10, OutboxMaxRetries = 2 },
            NullLogger<OutboxPublisherHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await PollingHelpers.WaitUntilAsync(() => publisher.AttemptCount(failingEventType) >= 2);
        // Dead işaretlemesi aynı tick'te SaveChanges ile commit edilir; birkaç ek tick bekleyip
        // artık taranmadığını (attempt sayısının donduğunu) da doğrula.
        var attemptsAtDead = publisher.AttemptCount(failingEventType);
        await Task.Delay(300);
        await service.StopAsync(CancellationToken.None);

        await using var verify = TestDbContextFactory.Create(connectionString);
        var row = await verify.OutboxMessages.SingleAsync(m => m.EventType == failingEventType);
        Assert.True(row.IsDead);
        Assert.True(row.RetryCount >= 2);
        Assert.Null(row.ProcessedAt);
        Assert.Equal(attemptsAtDead, publisher.AttemptCount(failingEventType));
    }

    [DockerFact]
    public async Task Relay_DeletesProcessedRows_OlderThanRetention()
    {
        var connectionString = UniqueDatabase();
        var oldEventType = $"outboxtest/retention/old/{Guid.NewGuid():N}";
        var recentEventType = $"outboxtest/retention/recent/{Guid.NewGuid():N}";

        await using (var seed = TestDbContextFactory.Create(connectionString))
        {
            var old = NewMessage(oldEventType);
            old.ProcessedAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(2);
            var recent = NewMessage(recentEventType);
            recent.ProcessedAt = DateTimeOffset.UtcNow;
            seed.OutboxMessages.AddRange(old, recent);
            await seed.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher();
        await using var provider = BuildProvider(connectionString, publisher);
        var service = new OutboxPublisherHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new RabbitMqOptions { OutboxPollingInterval = TimeSpan.FromMilliseconds(50), OutboxBatchSize = 10, OutboxRetention = TimeSpan.FromHours(1) },
            NullLogger<OutboxPublisherHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await PollingHelpers.WaitUntilAsync(async () => !await ExistsAsync(connectionString, oldEventType));
        await service.StopAsync(CancellationToken.None);

        Assert.True(await ExistsAsync(connectionString, recentEventType), "Retention penceresinden yeni satır yanlışlıkla silindi.");
    }

    [DockerFact]
    public async Task Relay_TwoConcurrentInstances_NeverPublishSameRowTwice()
    {
        var connectionString = UniqueDatabase();
        var eventTypes = Enumerable.Range(0, 20).Select(i => $"outboxtest/concurrent/{i}/{Guid.NewGuid():N}").ToList();

        await using (var seed = TestDbContextFactory.Create(connectionString))
        {
            seed.OutboxMessages.AddRange(eventTypes.Select(NewMessage));
            await seed.SaveChangesAsync();
        }

        var publisherA = new RecordingPublisher();
        var publisherB = new RecordingPublisher();
        var options = new RabbitMqOptions { OutboxPollingInterval = TimeSpan.FromMilliseconds(50), OutboxBatchSize = 5 };

        await using var providerA = BuildProvider(connectionString, publisherA);
        await using var providerB = BuildProvider(connectionString, publisherB);
        var serviceA = new OutboxPublisherHostedService(
            providerA.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<OutboxPublisherHostedService>.Instance);
        var serviceB = new OutboxPublisherHostedService(
            providerB.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<OutboxPublisherHostedService>.Instance);

        await serviceA.StartAsync(CancellationToken.None);
        await serviceB.StartAsync(CancellationToken.None);

        await PollingHelpers.WaitUntilAsync(
            () => publisherA.PublishedEventTypes.Count + publisherB.PublishedEventTypes.Count >= eventTypes.Count,
            timeout: TimeSpan.FromSeconds(15));

        await serviceA.StopAsync(CancellationToken.None);
        await serviceB.StopAsync(CancellationToken.None);

        var all = publisherA.PublishedEventTypes.Concat(publisherB.PublishedEventTypes).ToList();
        Assert.Equal(eventTypes.Count, all.Count);
        Assert.Equal(eventTypes.Count, all.Distinct().Count());
    }

    private static OutboxMessage NewMessage(string eventType) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = eventType,
        OccurredAt = DateTimeOffset.UtcNow,
        Payload = "{}",
    };

    private static async Task<bool> ExistsAsync(string connectionString, string eventType)
    {
        await using var context = TestDbContextFactory.Create(connectionString);
        return await context.OutboxMessages.AnyAsync(m => m.EventType == eventType);
    }

    private string UniqueDatabase() => TestDbContextFactory.UniqueConnectionString(_fixture.ConnectionString, "outbox");

    private static ServiceProvider BuildProvider(string connectionString, IRabbitMqPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<BaseForgeDbContext>(sp => sp.GetRequiredService<TestDbContext>());
        services.AddSingleton(publisher);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingPublisher(string? failEventType = null) : IRabbitMqPublisher
    {
        private readonly Lock _gate = new();
        private readonly List<string> _published = [];
        private readonly Dictionary<string, int> _attempts = [];

        public IReadOnlyList<string> PublishedEventTypes
        {
            get
            {
                lock (_gate)
                {
                    return [.. _published];
                }
            }
        }

        public int AttemptCount(string eventType)
        {
            lock (_gate)
            {
                return _attempts.GetValueOrDefault(eventType);
            }
        }

        public Task PublishRawAsync(string eventType, string envelopeJson, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _attempts[eventType] = _attempts.GetValueOrDefault(eventType) + 1;
            }

            if (eventType == failEventType)
            {
                throw new InvalidOperationException("Simulated publish failure.");
            }

            lock (_gate)
            {
                _published.Add(eventType);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestEvent(string eventType) : IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

        public string EventType { get; } = eventType;
    }
}
