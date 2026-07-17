using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Logging;
using BaseForge.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.UnitTests.Messaging;

/// <summary>
/// <see cref="OutboxEventBus"/>'ın olayı DB'ye yazmadan (yalnızca change tracker'a ekleyerek)
/// kuyruğa aldığını ve <see cref="OutboxMessage"/>'ın global query filter'lardan (soft-delete)
/// muaf olduğunu (marker interface implemente etmediği için) EF Core InMemory provider ile
/// doğrular.
/// </summary>
public sealed class OutboxEventBusTests
{
    [Fact]
    public async Task PublishAsync_OnlyTracksRow_DoesNotWriteUntilSaveChanges()
    {
        using var context = CreateContext(nameof(PublishAsync_OnlyTracksRow_DoesNotWriteUntilSaveChanges));
        var bus = new OutboxEventBus(context, new CorrelationIdAccessor());
        var integrationEvent = new TestEvent { Text = "hello" };

        await bus.PublishAsync(integrationEvent);

        // SaveChangesAsync henüz çağrılmadı -> DB'de (InMemory store'da) satır olmamalı.
        Assert.Empty(await context.OutboxMessages.AsNoTracking().ToListAsync());
        // Ama change tracker'da Added durumunda bekliyor olmalı (aynı SaveChanges'e bineceği için).
        var entry = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, entry.State);

        await context.SaveChangesAsync();

        var saved = Assert.Single(await context.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal(integrationEvent.EventId, saved.EventId);
        Assert.Equal(integrationEvent.EventType, saved.EventType);
        Assert.Null(saved.ProcessedAt);
        Assert.Equal(0, saved.RetryCount);
        Assert.Contains("hello", saved.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_RidesSameTransactionAsBusinessEntity()
    {
        using var context = CreateContext(nameof(PublishAsync_RidesSameTransactionAsBusinessEntity));
        var bus = new OutboxEventBus(context, new CorrelationIdAccessor());

        context.Widgets.Add(new Widget { Name = "gadget" });
        await bus.PublishAsync(new TestEvent { Text = "created" });

        // Business entity + outbox satırı TEK SaveChangesAsync çağrısıyla birlikte yazılmalı.
        await context.SaveChangesAsync();

        Assert.Single(await context.Widgets.AsNoTracking().ToListAsync());
        Assert.Single(await context.OutboxMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PublishAsync_EmbedsCurrentCorrelationId_IntoEnvelopePayload()
    {
        using var context = CreateContext(nameof(PublishAsync_EmbedsCurrentCorrelationId_IntoEnvelopePayload));
        var accessor = new CorrelationIdAccessor { Current = "test-correlation-id" };
        var bus = new OutboxEventBus(context, accessor);

        await bus.PublishAsync(new TestEvent { Text = "hello" });
        await context.SaveChangesAsync();

        var saved = Assert.Single(await context.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Contains("\"CorrelationId\":\"test-correlation-id\"", saved.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxMessage_IsNotSoftDeleted_PhysicallyRemovedOnDelete()
    {
        // OutboxMessage ISoftDelete implemente etmiyor: ApplyAuditAndSoftDelete'in
        // Deleted -> Modified+IsDeleted dönüşümü hiç devreye girmemeli, kayıt gerçekten silinmeli.
        using var context = CreateContext(nameof(OutboxMessage_IsNotSoftDeleted_PhysicallyRemovedOnDelete));
        var message = new OutboxMessage
        {
            EventId = Guid.NewGuid(),
            EventType = "unittests/Test",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = "{}",
        };
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        context.OutboxMessages.Remove(message);
        await context.SaveChangesAsync();

        Assert.Empty(await context.OutboxMessages.AsNoTracking().ToListAsync());
    }

    private static TestDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestEvent : IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

        public string EventType => "unittests/Test";

        public string Text { get; set; } = string.Empty;
    }

    private sealed class Widget
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext : BaseForgeDbContext
    {
        public TestDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Widget> Widgets => Set<Widget>();
    }
}
