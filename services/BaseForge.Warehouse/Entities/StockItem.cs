using BaseForge.Core.Entities;

namespace Warehouse.Entities;

/// <summary>StockItem entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
public sealed class StockItem : BaseEntity
{
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>Location.</summary>
    public string Location { get; set; } = string.Empty;
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
}
