using BaseForge.Core.CQRS;
using BaseForge.Core.Exceptions;
using BaseForge.Core.Interfaces;
using Orders.Entities;

namespace Orders.Features.OrderItems;

/// <summary>Yeni bir OrderItem oluşturur; üretilen kimliği döndürür.</summary>
public sealed class CreateOrderItemCommand : ICommand<Guid>
{
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>UnitPrice.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>OrderId.</summary>
    public Guid OrderId { get; set; }
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
}

internal sealed class CreateOrderItemHandler : ICommandHandler<CreateOrderItemCommand, Guid>
{
    private readonly IRepository<OrderItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderItemHandler(IRepository<OrderItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateOrderItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = new OrderItem
        {
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            OrderId = request.OrderId,
            ProductId = request.ProductId,
        };
        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>Var olan bir OrderItem kaydını günceller.</summary>
public sealed class UpdateOrderItemCommand : ICommand
{
    /// <summary>Güncellenecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Quantity.</summary>
    public int Quantity { get; set; }
    /// <summary>UnitPrice.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>OrderId.</summary>
    public Guid OrderId { get; set; }
    /// <summary>ProductId.</summary>
    public Guid ProductId { get; set; }
}

internal sealed class UpdateOrderItemHandler : ICommandHandler<UpdateOrderItemCommand>
{
    private readonly IRepository<OrderItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderItemHandler(IRepository<OrderItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateOrderItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OrderItem", request.Id);
        entity.Quantity = request.Quantity;
        entity.UnitPrice = request.UnitPrice;
        entity.OrderId = request.OrderId;
        entity.ProductId = request.ProductId;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Bir OrderItem kaydını siler (soft delete).</summary>
public sealed class DeleteOrderItemCommand : ICommand
{
    /// <summary>Silinecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class DeleteOrderItemHandler : ICommandHandler<DeleteOrderItemCommand>
{
    private readonly IRepository<OrderItem> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteOrderItemHandler(IRepository<OrderItem> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteOrderItemCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("OrderItem", request.Id);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
