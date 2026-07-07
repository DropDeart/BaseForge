using Warehouse.Entities;

namespace Warehouse.Features.StockItems;

/// <summary>StockItem veri transfer nesnesi.</summary>
public sealed class StockItemDto
{
    /// <summary>Kayıt kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>Location.</summary>
    public string Location { get; set; } = string.Empty;
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Bir StockItem entity'sinden DTO üretir.</summary>
    public static StockItemDto From(StockItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new StockItemDto
        {
            Id = entity.Id,
            Quantity = entity.Quantity,
            Location = entity.Location,
            ProductId = entity.ProductId,
        };
    }
}
