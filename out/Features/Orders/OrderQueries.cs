using BaseForge.Core.CQRS;
using BaseForge.Core.Interfaces;
using Orders.Entities;

namespace Orders.Features.Orders;

/// <summary>Kimliğe göre tek bir Order getirir.</summary>
public sealed class GetOrderByIdQuery : IQuery<OrderDto?>
{
    /// <summary>Aranan kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IRepository<Order> _repository;

    public GetOrderByIdHandler(IRepository<Order> repository) => _repository = repository;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : OrderDto.From(entity);
    }
}

/// <summary>Tüm Order kayıtlarını getirir.</summary>
public sealed class ListOrderQuery : IQuery<IReadOnlyList<OrderDto>>;

internal sealed class ListOrderHandler : IQueryHandler<ListOrderQuery, IReadOnlyList<OrderDto>>
{
    private readonly IRepository<Order> _repository;

    public ListOrderHandler(IRepository<Order> repository) => _repository = repository;

    public async Task<IReadOnlyList<OrderDto>> Handle(ListOrderQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAllAsync(cancellationToken);
        return items.Select(OrderDto.From).ToList();
    }
}
