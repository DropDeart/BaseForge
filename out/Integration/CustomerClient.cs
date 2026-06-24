namespace Orders.Integration;

/// <summary>
/// customers/Customer servisine senkron (gRPC) erişim sözleşmesi (stub).
/// Gerçek gRPC istemcisi ve .proto dosyası ayrıca eklenmelidir; bu servis
/// uzak kaydın yalnızca kimliğini tutar (cross-DB FK yoktur).
/// </summary>
public interface ICustomerClient
{
    /// <summary>Uzak servisten Customer referansını getirir.</summary>
    Task<CustomerReference?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Uzak Customer kaydının yerel referans görünümü.</summary>
public sealed class CustomerReference
{
    /// <summary>Uzak kaydın kimliği.</summary>
    public Guid Id { get; set; }
}
