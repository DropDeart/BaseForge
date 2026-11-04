using System.Text.Json;
using BaseForge.Core.Entities;
using BaseForge.Core.Interfaces;
using BaseForge.Core.Messaging;
using BaseForge.Core.MultiTenancy;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Logging;
using BaseForge.Infrastructure.Messaging;
using BaseForge.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.IntegrationTests.Persistence;

/// <summary>Kiracı filtresi, kiracılar arası okuma (MT-5), erken damgalama (MT-2) ve outbox zarfında kiracı (MT-1).</summary>
public sealed class TenantIsolationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static TenantDbContext Create(string connectionString, FakeTenant tenant)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(connectionString).Options;
        var context = new TenantDbContext(options, tenant);
        context.Database.EnsureCreated();
        return context;
    }

    [DockerFact]
    public async Task TenantsAreIsolated_UnlessCrossTenantRead()
    {
        var cs = TestDbContextFactory.UniqueConnectionString(postgres.ConnectionString, "tenant");
        await using (var a = Create(cs, new FakeTenant(TenantA)))
        {
            var widget = new TenantWidget { Name = "A" };
            a.Widgets.Add(widget);
            Assert.Equal(TenantA, widget.TenantId);   // MT-2: SaveChanges beklenmeden damgalandı
            await a.SaveChangesAsync();
        }

        await using (var b = Create(cs, new FakeTenant(TenantB)))
        {
            b.Widgets.Add(new TenantWidget { Name = "B" });
            await b.SaveChangesAsync();
            Assert.Equal(["B"], await b.Widgets.Select(w => w.Name).ToListAsync());
        }

        await using var platform = Create(cs, new FakeTenant(null) { ReadsAllTenants = true });
        Assert.Equal(["A", "B"], await platform.Widgets.OrderBy(w => w.Name).Select(w => w.Name).ToListAsync());

        await using var noTenant = Create(cs, new FakeTenant(null));
        Assert.Empty(await noTenant.Widgets.ToListAsync());
    }

    [DockerFact]
    public async Task OutboxEnvelope_CarriesTenant()
    {
        var cs = TestDbContextFactory.UniqueConnectionString(postgres.ConnectionString, "tenant");
        await using var context = Create(cs, new FakeTenant(TenantA));

        await new OutboxEventBus(context, new CorrelationIdAccessor(), new FakeTenant(TenantA)).PublishAsync(new PingEvent());
        using (TenantScope.Enter(TenantB))
        {
            // ICurrentTenant yoksa ortam kapsamı (arka plan işi) kullanılır.
            await new OutboxEventBus(context, new CorrelationIdAccessor()).PublishAsync(new PingEvent());
        }

        var tenants = context.ChangeTracker.Entries<OutboxMessage>()
            .Select(e => JsonSerializer.Deserialize<EventEnvelope>(e.Entity.Payload)!.TenantId)
            .ToList();
        Assert.Equal([TenantA, TenantB], tenants);
    }

    private sealed class FakeTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId { get; } = tenantId;

        public bool ReadsAllTenants { get; init; }
    }

    private sealed class TenantDbContext(DbContextOptions options, ICurrentTenant tenant) : BaseForgeDbContext(options, null, tenant)
    {
        public DbSet<TenantWidget> Widgets => Set<TenantWidget>();
    }

    private sealed class TenantWidget : BaseEntity, ITenantEntity
    {
        public Guid TenantId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class PingEvent : IIntegrationEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

        public string EventType => "test/Ping";
    }
}
