using BaseForge.Core.CQRS;
using BaseForge.Core.Interfaces;
using Warehouse.Entities;

namespace Warehouse.Features.StockItems;

/// <summary>Kimliğe göre tek bir StockItem getirir.</summary>
public sealed class GetStockItemByIdQuery : IQuery<StockItemDto?>
{
    /// <summary>Aranan kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class GetStockItemByIdHandler : IQueryHandler<GetStockItemByIdQuery, StockItemDto?>
{
    private readonly IRepository<StockItem> _repository;

    public GetStockItemByIdHandler(IRepository<StockItem> repository) => _repository = repository;

    public async Task<StockItemDto?> Handle(GetStockItemByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : StockItemDto.From(entity);
    }
}

/// <summary>Tüm StockItem kayıtlarını getirir.</summary>
public sealed class ListStockItemQuery : IQuery<IReadOnlyList<StockItemDto>>;

internal sealed class ListStockItemHandler : IQueryHandler<ListStockItemQuery, IReadOnlyList<StockItemDto>>
{
    private readonly IRepository<StockItem> _repository;

    public ListStockItemHandler(IRepository<StockItem> repository) => _repository = repository;

    public async Task<IReadOnlyList<StockItemDto>> Handle(ListStockItemQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAllAsync(cancellationToken);
        return items.Select(StockItemDto.From).ToList();
    }
}
