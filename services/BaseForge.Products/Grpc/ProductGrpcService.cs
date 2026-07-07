using System.Globalization;
using Grpc.Core;
using MediatR;
using Products.Features.Products;

namespace Products.Grpc;

/// <summary>
/// Product entity'sine diğer servislerin salt-okunur gRPC erişimi
/// (BaseForge.CodeGen tarafından üretildi; mevcut CQRS sorgusu üzerinden veri okur).
/// </summary>
public sealed class ProductGrpcService(ISender sender) : ProductService.ProductServiceBase
{
    public override async Task<ProductMessage> GetById(ProductByIdRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Guid.TryParse(request.Id, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Geçersiz id."));
        }

        var value = await sender.Send(new GetProductByIdQuery { Id = id }, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"Product bulunamadı: {id}"));

        return new ProductMessage
        {
            Id = value.Id.ToString(),
            Name = value.Name,
            Description = value.Description,
            Price = value.Price.ToString(CultureInfo.InvariantCulture),
            Stock = value.Stock,
        };
    }
}
