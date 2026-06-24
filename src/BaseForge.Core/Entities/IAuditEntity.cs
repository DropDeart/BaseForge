namespace BaseForge.Core.Entities;

/// <summary>
/// Oluşturma/güncelleme denetim (audit) bilgisini taşıyan entity'leri işaretler.
/// Alanlar repository/UnitOfWork tarafından otomatik doldurulur.
/// </summary>
public interface IAuditEntity
{
    /// <summary>Kaydın oluşturulma zamanı (UTC).</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Kaydın son güncellenme zamanı (UTC). Hiç güncellenmediyse <see langword="null"/>.</summary>
    DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Kaydı oluşturan kullanıcının kimliği. Bilinmiyorsa <see langword="null"/>.</summary>
    string? CreatedBy { get; set; }
}
