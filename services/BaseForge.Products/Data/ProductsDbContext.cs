using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Products.Entities;

namespace Products.Data;

/// <summary>Products servisinin EF Core context'i.</summary>
public sealed class ProductsDbContext : BaseForgeDbContext
{
    /// <summary>Yeni bir ProductsDbContext oluşturur.</summary>
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options)
        : base(options)
    {
    }

    /// <summary>Product tablosu.</summary>
    public DbSet<Product> Products => Set<Product>();
}
