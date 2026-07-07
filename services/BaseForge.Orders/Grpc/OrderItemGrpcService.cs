using System.Globalization;
using Grpc.Core;
using MediatR;
using Orders.Features.OrderItems;

namespace Orders.Grpc;

/// <summary>
/// OrderItem entity'sine diğer servislerin salt-okunur gRPC erişimi
/// (BaseForge.CodeGen tarafından üretildi; mevcut CQRS sorgusu üzerinden veri okur).
/// </summary>
public sealed class OrderItemGrpcService(ISender sender) : OrderItemService.OrderItemServiceBase
{
    public override async Task<OrderItemMessage> GetById(OrderItemByIdRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.Id, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Geçersiz id."));
        }

        var value = await sender.Send(new GetOrderItemByIdQuery { Id = id }, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"OrderItem bulunamadı: {id}"));

        return new OrderItemMessage
        {
            Id = value.Id.ToString(),
            Quantity = value.Quantity,
            UnitPrice = value.UnitPrice.ToString(CultureInfo.InvariantCulture),
        };
    }
}
