using Orders.Entities;

namespace Orders.Features.Orders;

/// <summary>Order veri transfer nesnesi.</summary>
public sealed class OrderDto
{
    /// <summary>Kayıt kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Total.</summary>
    public decimal Total { get; set; }
    /// <summary>PlacedAt.</summary>
    public DateTimeOffset PlacedAt { get; set; }
    /// <summary>CustomerId.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Bir Order entity'sinden DTO üretir.</summary>
    public static OrderDto From(Order entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new OrderDto
        {
            Id = entity.Id,
            Status = entity.Status,
            Total = entity.Total,
            PlacedAt = entity.PlacedAt,
            CustomerId = entity.CustomerId,
        };
    }
}
