namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket özeti.</summary>
public sealed record TenantDto(Guid Id, string Name, string Slug, string? TaxNo, TenantStatus Status, DateTimeOffset CreatedAt)
{
    /// <summary>Entity'den.</summary>
    public static TenantDto From(Tenant t) => new(t.Id, t.Name, t.Slug, t.TaxNo, t.Status, t.CreatedAt);
}

/// <summary>Kullanıcının bir şirketle ilişkisi (üyelik, davet ya da istek).</summary>
public sealed record MyMembershipDto(Guid MembershipId, TenantDto Tenant, MembershipStatus Status, IReadOnlyList<string> Roles, bool IsDefault);

/// <summary>Şirket yöneticisinin gördüğü üye/davet/istek.</summary>
public sealed record MemberDto(
    Guid MembershipId,
    Guid? UserId,
    string? Email,
    string? Phone,
    string? FullName,
    MembershipStatus Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

/// <summary>Katılım kodu araması sonucu (yalnız ad).</summary>
public sealed record TenantLookupDto(string Name, string Slug);

/// <summary>Şirket açma isteği.</summary>
public sealed record CreateTenantRequest(string Name, string? TaxNo);

/// <summary>Şirket bilgisi güncelleme (kod/slug değişmez; katılım bağlantıları bozulmasın).</summary>
public sealed record UpdateTenantRequest(string Name, string? TaxNo);

/// <summary>Katılma isteği (şirket kodu ile).</summary>
public sealed record JoinTenantRequest(string Slug);

/// <summary>Davet: e-posta veya telefon ve şirket içi roller.</summary>
public sealed record InviteMemberRequest(string? Email, string? Phone, List<string> Roles);

/// <summary>Rol listesi.</summary>
public sealed record MemberRolesRequest(List<string> Roles);

/// <summary>Şirket onay/ret/askı kararı notu.</summary>
public sealed record ReviewTenantRequest(string? Note);
