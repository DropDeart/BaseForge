using BaseForge.Core.Entities;
using BaseForge.Infrastructure.Data;
using BaseForge.Tools;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.UnitTests.Tools;

public sealed class DbmlGeneratorTests
{
    [Fact]
    public void Generate_WithRelatedEntities_ProducesTablesAndReference()
    {
        using var context = CreateContext();

        var dbml = DbmlGenerator.Generate(context);

        // Tablolar (DbSet adlarından)
        Assert.Contains("Table \"Users\"", dbml, StringComparison.Ordinal);
        Assert.Contains("Table \"Orders\"", dbml, StringComparison.Ordinal);

        // Guid -> uuid (Npgsql tip eşlemesi), birincil anahtar işaretlemesi
        Assert.Contains("\"uuid\"", dbml, StringComparison.Ordinal);
        Assert.Contains("[pk", dbml, StringComparison.Ordinal);

        // FK: Orders.UserId -> Users.Id
        Assert.Contains("Ref:", dbml, StringComparison.Ordinal);
        Assert.Contains("\"Orders\".\"UserId\"", dbml, StringComparison.Ordinal);
        Assert.Contains("\"Users\".\"Id\"", dbml, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_IncludesBaseEntityAuditColumns()
    {
        using var context = CreateContext();

        var dbml = DbmlGenerator.Generate(context);

        Assert.Contains("\"CreatedAt\"", dbml, StringComparison.Ordinal);
        Assert.Contains("\"IsDeleted\"", dbml, StringComparison.Ordinal);
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=localhost;Database=baseforge_test;Username=u;Password=p")
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestUser : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
    }

    private sealed class TestOrder : BaseEntity
    {
        public Guid UserId { get; set; }

        public decimal Total { get; set; }

        public TestUser? User { get; set; }
    }

    private sealed class TestDbContext : BaseForgeDbContext
    {
        public TestDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<TestUser> Users => Set<TestUser>();

        public DbSet<TestOrder> Orders => Set<TestOrder>();
    }
}
