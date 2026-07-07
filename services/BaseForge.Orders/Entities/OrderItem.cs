using BaseForge.Core.Entities;

namespace Orders.Entities;

/// <summary>OrderItem entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
public sealed class OrderItem : BaseEntity
{
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>UnitPrice.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>OrderId.</summary>
    public Guid OrderId { get; set; }
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
    /// <summary>Order (servis içi ilişki).</summary>
    public Order? Order { get; set; }
}
