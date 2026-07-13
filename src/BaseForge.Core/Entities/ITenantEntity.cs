namespace BaseForge.Core.Entities;

/// <summary>
/// Çok kiracılı (multi-tenant) bir servisteki entity'leri işaretler. <see cref="TenantId"/>,
/// kayıt oluşturulurken o anki kiracıdan otomatik doldurulur ve sorgularda global query filter
/// ile o kiracıya scope edilir.
/// </summary>
public interface ITenantEntity
{
    /// <summary>Kaydın ait olduğu kiracının kimliği.</summary>
    Guid TenantId { get; set; }
}
