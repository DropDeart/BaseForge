using BaseForge.Core.CQRS;
using BaseForge.Core.Exceptions;
using BaseForge.Core.Interfaces;
using Warehouse.Entities;

namespace Warehouse.Features.StockItems;

/// <summary>Yeni bir StockItem oluşturur; üretilen kimliği döndürür.</summary>
public sealed class CreateStockItemCommand : ICommand<Guid>
{
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>Location.</summary>
    public string Location { get; set; } = string.Empty;
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
}

internal sealed class CreateStockItemHandler : ICommandHandler<CreateStockItemCommand, Guid>
{
    private readonly IRepository<StockItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStockItemHandler(IRepository<StockItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateStockItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = new StockItem
        {
            Quantity = request.Quantity,
            Location = request.Location,
            ProductId = request.ProductId,
        };
        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>Var olan bir StockItem kaydını günceller.</summary>
public sealed class UpdateStockItemCommand : ICommand
{
    /// <summary>Güncellenecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>Location.</summary>
    public string Location { get; set; } = string.Empty;
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
}

internal sealed class UpdateStockItemHandler : ICommandHandler<UpdateStockItemCommand>
{
    private readonly IRepository<StockItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStockItemHandler(IRepository<StockItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateStockItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.Id);
        entity.Quantity = request.Quantity;
        entity.Location = request.Location;
        entity.ProductId = request.ProductId;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Bir StockItem kaydını siler (soft delete).</summary>
public sealed class DeleteStockItemCommand : ICommand
{
    /// <summary>Silinecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class DeleteStockItemHandler : ICommandHandler<DeleteStockItemCommand>
{
    private readonly IRepository<StockItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteStockItemHandler(IRepository<StockItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteStockItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("StockItem", request.Id);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
