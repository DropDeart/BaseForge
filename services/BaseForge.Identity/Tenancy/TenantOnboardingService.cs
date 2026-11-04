using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket açma (self-servis) ve platform onayı.</summary>
public interface ITenantOnboardingService
{
    /// <summary>Şirketi açar; açan kişi şirket yöneticisi olur.</summary>
    Task<MyMembershipDto> CreateAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken);

    /// <summary>Şirketler (duruma göre süzülür).</summary>
    Task<IReadOnlyList<TenantDto>> ListAsync(TenantStatus? status, CancellationToken cancellationToken);

    /// <summary>Onay / ret / askı / yeniden etkinleştirme.</summary>
    Task<TenantDto> ReviewAsync(Guid tenantId, TenantStatus target, Guid reviewerId, string? note, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ITenantOnboardingService" />
internal sealed class TenantOnboardingService(IdentityServiceDbContext db, AuthOptions options, TimeProvider clock)
    : ITenantOnboardingService
{
    private readonly MultiTenancyOptions _tenancy = options.MultiTenancy;

    public async Task<MyMembershipDto> CreateAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 200)
        {
            throw IdentityException.Invalid("Şirket adı 2-200 karakter olmalı.");
        }

        var now = clock.GetUtcNow();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = await UniqueSlugAsync(name, cancellationToken),
            TaxNo = string.IsNullOrWhiteSpace(request.TaxNo) ? null : request.TaxNo.Trim(),
            Status = _tenancy.RequireApproval ? TenantStatus.Pending : TenantStatus.Active,
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Tenant = tenant,
            UserId = userId,
            Roles = [_tenancy.AdminRole],
            Status = MembershipStatus.Active,
            IsDefault = !await db.Set<TenantMembership>().AnyAsync(m => m.UserId == userId && m.IsDefault, cancellationToken),
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        db.Add(tenant);
        db.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return new MyMembershipDto(membership.Id, TenantDto.From(tenant), membership.Status, membership.Roles, membership.IsDefault);
    }

    public async Task<IReadOnlyList<TenantDto>> ListAsync(TenantStatus? status, CancellationToken cancellationToken)
        => await db.Set<Tenant>()
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => TenantDto.From(t))
            .ToListAsync(cancellationToken);

    public async Task<TenantDto> ReviewAsync(Guid tenantId, TenantStatus target, Guid reviewerId, string? note, CancellationToken cancellationToken)
    {
        var tenant = await db.Set<Tenant>().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw IdentityException.NotFound("Şirket bulunamadı.");

        tenant.Review(target, reviewerId, note, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }

    // Aynı addan ikinci şirket "-2", "-3" ... ile ayrışır.
    private async Task<string> UniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = TenancyText.Slug(name);
        if (baseSlug.Length == 0)
        {
            baseSlug = "sirket";
        }

        var taken = await db.Set<Tenant>()
            .Where(t => t.Slug == baseSlug || t.Slug.StartsWith(baseSlug + "-"))
            .Select(t => t.Slug)
            .ToListAsync(cancellationToken);

        var slug = baseSlug;
        for (var i = 2; taken.Contains(slug, StringComparer.Ordinal); i++)
        {
            slug = $"{baseSlug}-{i}";
        }

        return slug;
    }
}
