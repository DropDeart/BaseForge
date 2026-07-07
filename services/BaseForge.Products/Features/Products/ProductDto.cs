using Products.Entities;

namespace Products.Features.Products;

/// <summary>Product veri transfer nesnesi.</summary>
public sealed class ProductDto
{
    /// <summary>Kayıt kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Price.</summary>
    public decimal Price { get; set; }
    /// <summary>Stock.</summary>
    public int Stock { get; set; }

    /// <summary>Bir Product entity'sinden DTO üretir.</summary>
    public static ProductDto From(Product entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new ProductDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Stock = entity.Stock,
        };
    }
}
