using BaseForge.Core.Entities;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.IntegrationTests.Persistence;

/// <summary>
/// BaseForge.Infrastructure'ı GERÇEK PostgreSQL'e karşı doğrular (Testcontainers).
/// InMemory ile test edilemeyen Dapper ham SQL ve transaction yolları dahil.
/// </summary>
public sealed class RepositoryIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RepositoryIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    [DockerFact]
    public async Task Repository_AddAndGet_RoundtripsWithAudit()
    {
        await using var context = CreateContext();
        var repository = new GenericRepository<Product>(context);
        var unitOfWork = new UnitOfWork(context);

        var product = new Product { Name = "Kalem", Price = 9.5m };
        await repository.AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(product.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Kalem", loaded!.Name);
        Assert.Equal(9.5m, loaded.Price);
        Assert.NotEqual(default, loaded.CreatedAt);
    }

    [DockerFact]
    public async Task Delete_SoftDeletes_AndQueryFilterHides()
    {
        await using var context = CreateContext();
        var repository = new GenericRepository<Product>(context);
        var unitOfWork = new UnitOfWork(context);

        var product = new Product { Name = "Silinecek", Price = 1m };
        await repository.AddAsync(product);
        await unitOfWork.SaveChangesAsync();

        await repository.DeleteAsync(product);
        await unitOfWork.SaveChangesAsync();

        var visible = await repository.ListAllAsync();
        Assert.DoesNotContain(visible, x => x.Id == product.Id);

        var includingDeleted = await context.Set<Product>().IgnoreQueryFilters().ToListAsync();
        Assert.Contains(includingDeleted, x => x.Id == product.Id && x.IsDeleted);
    }

    [DockerFact]
    public async Task Dapper_RawSql_ReturnsRows()
    {
        await using var context = CreateContext();
        var repository = new GenericRepository<Product>(context);
        var unitOfWork = new UnitOfWork(context);

        await repository.AddAsync(new Product { Name = "Defter", Price = 20m });
        await unitOfWork.SaveChangesAsync();

        var sqlQuery = new DapperSqlQuery(context);
        var names = await sqlQuery.QueryAsync<string>(
            "SELECT \"Name\" FROM \"Products\" WHERE \"IsDeleted\" = false");

        Assert.Contains("Defter", names);
    }

    [DockerFact]
    public async Task UnitOfWork_Rollback_DoesNotPersist()
    {
        await using (var context = CreateContext())
        {
            var repository = new GenericRepository<Product>(context);
            var unitOfWork = new UnitOfWork(context);

            await unitOfWork.BeginTransactionAsync();
            await repository.AddAsync(new Product { Name = "Gecici", Price = 5m });
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.RollbackAsync();
        }

        await using var verifyContext = CreateContext();
        var all = await new GenericRepository<Product>(verifyContext).ListAllAsync();
        Assert.DoesNotContain(all, x => x.Name == "Gecici");
    }

    private TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class Product : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    private sealed class TestDbContext : BaseForgeDbContext
    {
        public TestDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
    }
}
