using BaseForge.Identity.ErrorHandling;

namespace BaseForge.Identity.Tenancy;

/// <summary>Kullanıcının bir şirketteki üyeliği; davet ve katılma isteği de bir üyelik kaydıdır.</summary>
public sealed class TenantMembership
{
    /// <summary>Kimlik.</summary>
    public Guid Id { get; set; }

    /// <summary>Şirket.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Şirket (navigation).</summary>
    public Tenant? Tenant { get; set; }

    /// <summary>Kullanıcı; kayıtlı olmayan birine davette null.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Kayıtlı olmayana davetin e-postası.</summary>
    public string? InvitedEmail { get; set; }

    /// <summary>Kayıtlı olmayana davetin telefonu.</summary>
    public string? InvitedPhone { get; set; }

    /// <summary>Şirket içi roller.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Durum.</summary>
    public MembershipStatus Status { get; set; }

    /// <summary>Girişte varsayılan şirket mi?</summary>
    public bool IsDefault { get; set; }

    /// <summary>Oluşturma zamanı.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Daveti/isteği oluşturan.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Kabul/ret/çıkarma zamanı.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>Henüz sonuçlanmamış ya da aktif mi?</summary>
    public bool IsOpen => Status is MembershipStatus.Invited or MembershipStatus.Requested or MembershipStatus.Active;

    /// <summary>Kişi daveti kabul eder.</summary>
    public void Accept(DateTimeOffset at) => Move(MembershipStatus.Invited, MembershipStatus.Active, at);

    /// <summary>Kişi daveti reddeder.</summary>
    public void Decline(DateTimeOffset at) => Move(MembershipStatus.Invited, MembershipStatus.Declined, at);

    /// <summary>Şirket katılma isteğini rollerle onaylar.</summary>
    public void Approve(IReadOnlyList<string> roles, DateTimeOffset at)
    {
        Move(MembershipStatus.Requested, MembershipStatus.Active, at);
        Roles = [.. roles];
    }

    /// <summary>Şirket katılma isteğini reddeder.</summary>
    public void Reject(DateTimeOffset at) => Move(MembershipStatus.Requested, MembershipStatus.Rejected, at);

    /// <summary>Şirketten çıkarma, ayrılma ya da daveti geri alma.</summary>
    public void Remove(DateTimeOffset at)
    {
        if (!IsOpen)
        {
            throw IdentityException.Conflict("Üyelik zaten kapalı.");
        }

        Status = MembershipStatus.Removed;
        IsDefault = false;
        RespondedAt = at;
    }

    private void Move(MembershipStatus from, MembershipStatus to, DateTimeOffset at)
    {
        if (Status != from)
        {
            throw IdentityException.Conflict($"Üyelik {Status} durumunda; bu işlem yapılamaz.");
        }

        Status = to;
        RespondedAt = at;
    }
}

/// <summary>Üyelik durumu.</summary>
public enum MembershipStatus
{
    /// <summary>Şirket davet etti, kişinin kabulü bekleniyor.</summary>
    Invited,

    /// <summary>Kişi katılmak istedi, şirketin onayı bekleniyor.</summary>
    Requested,

    /// <summary>Üye.</summary>
    Active,

    /// <summary>Kişi daveti reddetti.</summary>
    Declined,

    /// <summary>Şirket isteği reddetti.</summary>
    Rejected,

    /// <summary>Çıkarıldı / ayrıldı / davet geri alındı.</summary>
    Removed,
}
