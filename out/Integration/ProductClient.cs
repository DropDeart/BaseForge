namespace Orders.Integration;

/// <summary>
/// catalog/Product servisine senkron (gRPC) erişim sözleşmesi (stub).
/// Gerçek gRPC istemcisi ve .proto dosyası ayrıca eklenmelidir; bu servis
/// uzak kaydın yalnızca kimliğini tutar (cross-DB FK yoktur).
/// </summary>
public interface IProductClient
{
    /// <summary>Uzak servisten Product referansını getirir.</summary>
    Task<ProductReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Uzak Product kaydının yerel referans görünümü.</summary>
public sealed class ProductReference
{
    /// <summary>Uzak kaydın kimliği.</summary>
    public Guid Id { get; set; }
}
