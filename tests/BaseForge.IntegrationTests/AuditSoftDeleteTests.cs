using BaseForge.Core.Entities;
using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.IntegrationTests;

/// <summary>
/// <see cref="BaseForgeDbContext"/>'in audit doldurma ve soft-delete davranışını EF Core
/// InMemory sağlayıcısıyla doğrular. (Gerçek PostgreSQL'e karşı testler Faz 5'te
/// Testcontainers ile eklenecektir.)
/// </summary>
public sealed class AuditSoftDeleteTests
{
    [Fact]
    public async Task SaveChanges_OnAdd_SetsCreatedAt()
    {
        await using var context = CreateContext();
        var item = new Item { Name = "alpha" };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        Assert.NotEqual(default, item.CreatedAt);
        Assert.Null(item.UpdatedAt);
        Assert.False(item.IsDeleted);
    }

    [Fact]
    public async Task SaveChanges_OnModify_SetsUpdatedAt()
    {
        await using var context = CreateContext();
        var item = new Item { Name = "alpha" };
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.Name = "beta";
        await context.SaveChangesAsync();

        Assert.NotNull(item.UpdatedAt);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndHidesFromQueries()
    {
        await using var context = CreateContext();
        var item = new Item { Name = "alpha" };
        context.Items.Add(item);
        await context.SaveChangesAsync();

        context.Items.Remove(item);
        await context.SaveChangesAsync();

        // Global query filter: silinmiş kayıt normal sorguda görünmez.
        var visible = await context.Items.ToListAsync();
        Assert.Empty(visible);

        // Fiziksel olarak hâlâ duruyor ve soft-delete işaretli.
        var all = await context.Items.IgnoreQueryFilters().ToListAsync();
        Assert.Single(all);
        Assert.True(all[0].IsDeleted);
        Assert.NotNull(all[0].DeletedAt);
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"baseforge-{Guid.NewGuid()}")
            .Options;
        return new TestDbContext(options);
    }

    private sealed class Item : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext : BaseForgeDbContext
    {
        public TestDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Item> Items => Set<Item>();
    }
}
