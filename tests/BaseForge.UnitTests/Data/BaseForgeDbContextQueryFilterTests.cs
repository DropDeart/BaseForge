using BaseForge.Core.Entities;
using BaseForge.Core.Interfaces;
using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.UnitTests.Data;

/// <summary>
/// <see cref="BaseForgeDbContext.OnModelCreating"/>'in <see cref="ISoftDelete"/> ve
/// <see cref="ITenantEntity"/>'yi birleştiren global query filter mantığını, EF Core InMemory
/// provider üzerinden 4 senaryoda (ne biri ne diğeri / yalnız soft-delete / yalnız tenant /
/// ikisi birden) doğrular.
/// </summary>
public sealed class BaseForgeDbContextQueryFilterTests
{
    [Fact]
    public async Task NoInterfaces_ReturnsAllRows()
    {
        using var context = CreateContext(nameof(NoInterfaces_ReturnsAllRows), currentTenant: null);
        context.PlainEntities.AddRange(new PlainEntity { Name = "a" }, new PlainEntity { Name = "b" });
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.PlainEntities.CountAsync());
    }

    [Fact]
    public async Task SoftDeleteOnly_ExcludesDeletedRows()
    {
        using var context = CreateContext(nameof(SoftDeleteOnly_ExcludesDeletedRows), currentTenant: null);
        var kept = new SoftDeleteOnlyEntity { Name = "kept" };
        var removed = new SoftDeleteOnlyEntity { Name = "removed" };
        context.SoftDeleteOnlyEntities.AddRange(kept, removed);
        await context.SaveChangesAsync();

        context.SoftDeleteOnlyEntities.Remove(removed);
        await context.SaveChangesAsync();

        var remaining = await context.SoftDeleteOnlyEntities.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("kept", remaining[0].Name);
    }

    [Fact]
    public async Task TenantOnly_ScopesToCurrentTenant()
    {
        var dbName = nameof(TenantOnly_ScopesToCurrentTenant);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var writer = CreateContext(dbName, new FakeCurrentTenant(tenantA)))
        {
            writer.TenantOnlyEntities.AddRange(new TenantOnlyEntity { Name = "a1" }, new TenantOnlyEntity { Name = "a2" });
            await writer.SaveChangesAsync();
        }

        using (var writer = CreateContext(dbName, new FakeCurrentTenant(tenantB)))
        {
            writer.TenantOnlyEntities.Add(new TenantOnlyEntity { Name = "b1" });
            await writer.SaveChangesAsync();
        }

        using var readerA = CreateContext(dbName, new FakeCurrentTenant(tenantA));
        Assert.Equal(2, await readerA.TenantOnlyEntities.CountAsync());

        using var readerB = CreateContext(dbName, new FakeCurrentTenant(tenantB));
        Assert.Equal(1, await readerB.TenantOnlyEntities.CountAsync());
    }

    [Fact]
    public async Task Both_CombinesSoftDeleteAndTenantFilters()
    {
        var dbName = nameof(Both_CombinesSoftDeleteAndTenantFilters);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        BothEntity tenantAKept;
        BothEntity tenantARemoved;
        using (var writer = CreateContext(dbName, new FakeCurrentTenant(tenantA)))
        {
            tenantAKept = new BothEntity { Name = "kept" };
            tenantARemoved = new BothEntity { Name = "removed" };
            writer.BothEntities.AddRange(tenantAKept, tenantARemoved);
            await writer.SaveChangesAsync();
        }

        using (var writer = CreateContext(dbName, new FakeCurrentTenant(tenantB)))
        {
            writer.BothEntities.Add(new BothEntity { Name = "other-tenant" });
            await writer.SaveChangesAsync();
        }

        using (var writer = CreateContext(dbName, new FakeCurrentTenant(tenantA)))
        {
            var toRemove = await writer.BothEntities.SingleAsync(e => e.Name == "removed");
            writer.BothEntities.Remove(toRemove);
            await writer.SaveChangesAsync();
        }

        using var reader = CreateContext(dbName, new FakeCurrentTenant(tenantA));
        var visible = await reader.BothEntities.ToListAsync();
        Assert.Single(visible);
        Assert.Equal("kept", visible[0].Name);
    }

    [Fact]
    public async Task TenantEntity_AddedWithoutCurrentTenant_Throws()
    {
        using var context = CreateContext(nameof(TenantEntity_AddedWithoutCurrentTenant_Throws), currentTenant: null);
        context.TenantOnlyEntities.Add(new TenantOnlyEntity { Name = "orphan" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    private static FilterTestDbContext CreateContext(string dbName, ICurrentTenant? currentTenant)
    {
        var options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new FilterTestDbContext(options, currentTenant);
    }

    private sealed class FakeCurrentTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class PlainEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;
    }

    private sealed class SoftDeleteOnlyEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TenantOnlyEntity : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class BothEntity : BaseEntity, ITenantEntity
    {
        public Guid TenantId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class FilterTestDbContext : BaseForgeDbContext
    {
        public FilterTestDbContext(DbContextOptions options, ICurrentTenant? currentTenant)
            : base(options, currentUser: null, currentTenant: currentTenant)
        {
        }

        public DbSet<PlainEntity> PlainEntities => Set<PlainEntity>();

        public DbSet<SoftDeleteOnlyEntity> SoftDeleteOnlyEntities => Set<SoftDeleteOnlyEntity>();

        public DbSet<TenantOnlyEntity> TenantOnlyEntities => Set<TenantOnlyEntity>();

        public DbSet<BothEntity> BothEntities => Set<BothEntity>();
    }
}
