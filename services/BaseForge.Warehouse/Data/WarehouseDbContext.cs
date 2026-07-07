using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Warehouse.Entities;

namespace Warehouse.Data;

/// <summary>Warehouse servisinin EF Core context'i.</summary>
public sealed class WarehouseDbContext : BaseForgeDbContext
{
    /// <summary>Yeni bir WarehouseDbContext oluşturur.</summary>
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options)
        : base(options)
    {
    }

    /// <summary>StockItem tablosu.</summary>
    public DbSet<StockItem> StockItems => Set<StockItem>();
}
