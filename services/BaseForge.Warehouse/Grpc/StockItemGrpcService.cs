using System.Globalization;
using Grpc.Core;
using MediatR;
using Warehouse.Features.StockItems;

namespace Warehouse.Grpc;

/// <summary>
/// StockItem entity'sine diğer servislerin salt-okunur gRPC erişimi
/// (BaseForge.CodeGen tarafından üretildi; mevcut CQRS sorgusu üzerinden veri okur).
/// </summary>
public sealed class StockItemGrpcService(ISender sender) : StockItemService.StockItemServiceBase
{
    public override async Task<StockItemMessage> GetById(StockItemByIdRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.Id, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Geçersiz id."));
        }

        var value = await sender.Send(new GetStockItemByIdQuery { Id = id }, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"StockItem bulunamadı: {id}"));

        return new StockItemMessage
        {
            Id = value.Id.ToString(),
            Quantity = value.Quantity,
            Location = value.Location,
        };
    }
}
