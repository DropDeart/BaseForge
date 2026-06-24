namespace BaseForge.Core.Entities;

/// <summary>
/// Mantıksal (soft) silmeyi destekleyen entity'leri işaretler. Kayıt fiziksel olarak
/// silinmez; <see cref="IsDeleted"/> işaretlenir ve sorgularda filtrelenir.
/// </summary>
public interface ISoftDelete
{
    /// <summary>Kayıt mantıksal olarak silinmişse <see langword="true"/>.</summary>
    bool IsDeleted { get; set; }

    /// <summary>Kaydın silinme zamanı (UTC). Silinmemişse <see langword="null"/>.</summary>
    DateTimeOffset? DeletedAt { get; set; }
}
