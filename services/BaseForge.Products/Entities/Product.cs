using BaseForge.Core.Entities;

namespace Products.Entities;

/// <summary>Product entity'si (BaseForge.CodeGen tarafından üretildi).</summary>
public sealed class Product : BaseEntity
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Price.</summary>
    public decimal Price { get; set; }
    /// <summary>Stock.</summary>
    public int Stock { get; set; }
}
