using BaseForge.Core.Entities;

namespace Orders.Entities;

/// <summary>Order entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
public sealed class Order : BaseEntity
{
    /// <summary>Status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Total.</summary>
    public decimal Total { get; set; }
    /// <summary>PlacedAt.</summary>
    public DateTimeOffset PlacedAt { get; set; }
    /// <summary>UserId.</summary>
    public Guid UserId { get; set; }
    /// <summary>Items (servis içi ilişki).</summary>
    public ICollection<OrderItem> Items { get; } = [];
}
