using System.Globalization;
using Grpc.Core;
using Products.Grpc;

namespace Warehouse.Integration;

/// <summary>products/Product servisine senkron (gRPC) erişim sözleşmesi. BaseForge.CodeGen tarafından üretildi.</summary>
public interface IProductClient
{
    /// <summary>Uzak servisten Product kaydını getirir; bulunamazsa <see langword="null"/>.</summary>
    Task<ProductReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Uzak Product kaydının yerel (zengin) görünümü.</summary>
public sealed class ProductReference
{
    /// <summary>Uzak kaydın kimliği.</summary>
    public Guid Id { get; set; }
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Price.</summary>
    public decimal Price { get; set; }
    /// <summary>Stock.</summary>
    public int Stock { get; set; }
}

/// <summary><see cref="IProductClient"/>'in Products servisine gRPC ile bağlanan gerçek implementasyonu.</summary>
public sealed class ProductClient(ProductService.ProductServiceClient client) : IProductClient
{
    public async Task<ProductReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetByIdAsync(
                new ProductByIdRequest { Id = id.ToString() },
                cancellationToken: cancellationToken);

            return new ProductReference
            {
                Id = Guid.Parse(response.Id),
                Name = response.Name,
                Description = response.Description,
                Price = decimal.Parse(response.Price, CultureInfo.InvariantCulture),
                Stock = response.Stock,
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
