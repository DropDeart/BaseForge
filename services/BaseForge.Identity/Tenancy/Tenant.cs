using BaseForge.Identity.ErrorHandling;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket (kiracı).</summary>
public sealed class Tenant
{
    /// <summary>Kimlik; token'daki tenant_id.</summary>
    public Guid Id { get; set; }

    /// <summary>Görünen ad.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Katılım kodu (benzersiz, URL dostu).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Vergi/kimlik no.</summary>
    public string? TaxNo { get; set; }

    /// <summary>Durum.</summary>
    public TenantStatus Status { get; set; }

    /// <summary>Açan kullanıcı.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Açılış zamanı.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Son onay/ret/askı zamanı.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Son karar veren.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>Karar notu (ret/askı gerekçesi).</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Durumu değiştirir; geçersiz geçişte 409.</summary>
    public void Review(TenantStatus target, Guid reviewerId, string? note, DateTimeOffset at)
    {
        var allowed = (Status, target) switch
        {
            (TenantStatus.Pending, TenantStatus.Active or TenantStatus.Rejected) => true,
            (TenantStatus.Active, TenantStatus.Suspended) => true,
            (TenantStatus.Suspended, TenantStatus.Active) => true,
            (TenantStatus.Rejected, TenantStatus.Active) => true,
            _ => false,
        };
        if (!allowed)
        {
            throw IdentityException.Conflict($"Şirket {Status} durumundan {target} durumuna geçemez.");
        }

        Status = target;
        ReviewedAt = at;
        ReviewedByUserId = reviewerId;
        ReviewNote = note;
    }
}

/// <summary>Şirket durumu.</summary>
public enum TenantStatus
{
    /// <summary>Onay bekliyor.</summary>
    Pending,

    /// <summary>Aktif; üyeler token alabilir.</summary>
    Active,

    /// <summary>Reddedildi.</summary>
    Rejected,

    /// <summary>Askıya alındı.</summary>
    Suspended,
}
