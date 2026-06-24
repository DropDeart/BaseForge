using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Orders.Entities;

namespace Orders.Data;

/// <summary>Orders servisinin EF Core context'i.</summary>
public sealed class OrdersDbContext : BaseForgeDbContext
{
    /// <summary>Yeni bir OrdersDbContext oluşturur.</summary>
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    /// <summary>Order tablosu.</summary>
    public DbSet<Order> Orders => Set<Order>();
    /// <summary>OrderItem tablosu.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
}
