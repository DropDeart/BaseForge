using BaseForge.Core.CQRS;
using BaseForge.Core.Interfaces;
using Products.Entities;

namespace Products.Features.Products;

/// <summary>Kimliğe göre tek bir Product getirir.</summary>
public sealed class GetProductByIdQuery : IQuery<ProductDto?>
{
    /// <summary>Aranan kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class GetProductByIdHandler : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IRepository<Product> _repository;

    public GetProductByIdHandler(IRepository<Product> repository) => _repository = repository;

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : ProductDto.From(entity);
    }
}

/// <summary>Tüm Product kayıtlarını getirir.</summary>
public sealed class ListProductQuery : IQuery<IReadOnlyList<ProductDto>>;

internal sealed class ListProductHandler : IQueryHandler<ListProductQuery, IReadOnlyList<ProductDto>>
{
    private readonly IRepository<Product> _repository;

    public ListProductHandler(IRepository<Product> repository) => _repository = repository;

    public async Task<IReadOnlyList<ProductDto>> Handle(ListProductQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.ListAllAsync(cancellationToken);
        return items.Select(ProductDto.From).ToList();
    }
}
