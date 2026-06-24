using BaseForge.Core.CQRS;
using BaseForge.Core.Exceptions;
using BaseForge.Core.Interfaces;
using Orders.Entities;

namespace Orders.Features.Orders;

/// <summary>Yeni bir Order oluşturur; üretilen kimliği döndürür.</summary>
public sealed class CreateOrderCommand : ICommand<Guid>
{
    /// <summary>Status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Total.</summary>
    public decimal Total { get; set; }
    /// <summary>PlacedAt.</summary>
    public DateTimeOffset PlacedAt { get; set; }
    /// <summary>CustomerId.</summary>
    public Guid CustomerId { get; set; }
}

internal sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IRepository<Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderHandler(IRepository<Order> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = new Order
        {
            Status = request.Status,
            Total = request.Total,
            PlacedAt = request.PlacedAt,
            CustomerId = request.CustomerId,
        };
        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

/// <summary>Var olan bir Order kaydını günceller.</summary>
public sealed class UpdateOrderCommand : ICommand
{
    /// <summary>Güncellenecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Total.</summary>
    public decimal Total { get; set; }
    /// <summary>PlacedAt.</summary>
    public DateTimeOffset PlacedAt { get; set; }
    /// <summary>CustomerId.</summary>
    public Guid CustomerId { get; set; }
}

internal sealed class UpdateOrderHandler : ICommandHandler<UpdateOrderCommand>
{
    private readonly IRepository<Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrderHandler(IRepository<Order> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Order", request.Id);
        entity.Status = request.Status;
        entity.Total = request.Total;
        entity.PlacedAt = request.PlacedAt;
        entity.CustomerId = request.CustomerId;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Bir Order kaydını siler (soft delete).</summary>
public sealed class DeleteOrderCommand : ICommand
{
    /// <summary>Silinecek kaydın kimliği.</summary>
    public Guid Id { get; set; }
}

internal sealed class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand>
{
    private readonly IRepository<Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteOrderHandler(IRepository<Order> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Order", request.Id);
        await _repository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
