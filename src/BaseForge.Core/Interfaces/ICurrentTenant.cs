namespace BaseForge.Core.Interfaces;

/// <summary>
/// O anki istek bağlamındaki kiracıyı (tenant) temsil eder. Implementasyonu API katmanında
/// (örn. <c>HttpContext</c> / JWT claim'lerinden) sağlanır; <see cref="Entities.ITenantEntity"/>
/// alanlarını doldurmak ve sorguları kiracıya scope etmek için kullanılır.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>O anki kiracının kimliği; çözümlenemiyorsa (örn. anonim istek) <see langword="null"/>.</summary>
    Guid? TenantId { get; }
}
