using BaseForge.Core.CQRS;
using BaseForge.Core.Interfaces;
using Orders.Entities;

namespace Orders.Features.OrderItems;

/// <summary>Kimliğe göre tek bir OrderItem getirir.</summary>
public sealed class GetOrderItemByIdQuery : IQuery<OrderItemDto?>
{
    /// <summary>Aranan kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class GetOrderItemByIdHandler : IQueryHandler<GetOrderItemByIdQuery, OrderItemDto?>
{
    private readonly IRepository<OrderItem> _repository;

    public GetOrderItemByIdHandler(IRepository<OrderItem> repository) => _repository = repository;

    public async Task<OrderItemDto?> Handle(GetOrderItemByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : OrderItemDto.From(entity);
    }
}

/// <summary>Tüm OrderItem kayıtlarını getirir.</summary>
public sealed class ListOrderItemQuery : IQuery<IReadOnlyList<OrderItemDto>>;

internal sealed class ListOrderItemHandler : IQueryHandler<ListOrderItemQuery, IReadOnlyList<OrderItemDto>>
{
    private readonly IRepository<OrderItem> _repository;

    public ListOrderItemHandler(IRepository<OrderItem> repository) => _repository = repository;

    public async Task<IReadOnlyList<OrderItemDto>> Handle(ListOrderItemQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAllAsync(cancellationToken);
        return items.Select(OrderItemDto.From).ToList();
    }
}
