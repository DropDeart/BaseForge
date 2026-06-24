using Orders.Entities;

namespace Orders.Features.OrderItems;

/// <summary>OrderItem veri transfer nesnesi.</summary>
public sealed class OrderItemDto
{
    /// <summary>Kayıt kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>UnitPrice.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>OrderId.</summary>
    public Guid OrderId { get; set; }
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Bir OrderItem entity'sinden DTO üretir.</summary>
    public static OrderItemDto From(OrderItem entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new OrderItemDto
        {
            Id = entity.Id,
            Quantity = entity.Quantity,
            UnitPrice = entity.UnitPrice,
            OrderId = entity.OrderId,
            ProductId = entity.ProductId,
        };
    }
}
