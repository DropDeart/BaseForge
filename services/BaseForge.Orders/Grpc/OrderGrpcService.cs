using System.Globalization;
using Grpc.Core;
using MediatR;
using Orders.Features.Orders;

namespace Orders.Grpc;

/// <summary>
/// Order entity'sine diğer servislerin salt-okunur gRPC erişimi
/// (BaseForge.CodeGen tarafından üretildi; mevcut CQRS sorgusu üzerinden veri okur).
/// </summary>
public sealed class OrderGrpcService(ISender sender) : OrderService.OrderServiceBase
{
    public override async Task<OrderMessage> GetById(OrderByIdRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.Id, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Geçersiz id."));
        }

        var value = await sender.Send(new GetOrderByIdQuery { Id = id }, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Order bulunamadı: {id}"));

        return new OrderMessage
        {
            Id = value.Id.ToString(),
            Status = value.Status,
            Total = value.Total.ToString(CultureInfo.InvariantCulture),
            PlacedAt = value.PlacedAt.ToString("o", CultureInfo.InvariantCulture),
        };
    }
}
