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

    /// <summary>Şirketin adını ve vergi numarasını günceller (şirket yöneticisi ya da platform yöneticisi).</summary>
    Task<TenantDto> UpdateAsync(Guid tenantId, TenantActor actor, UpdateTenantRequest request, CancellationToken cancellationToken);

    /// <summary>Şirketler (duruma göre süzülür).</summary>
    Task<IReadOnlyList<TenantDto>> ListAsync(TenantStatus? status, CancellationToken cancellationToken);

    /// <summary>Onay / ret / askı / yeniden etkinleştirme.</summary>
    Task<TenantDto> ReviewAsync(Guid tenantId, TenantStatus target, Guid reviewerId, string? note, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ITenantOnboardingService" />
internal sealed class TenantOnboardingService(IdentityServiceDbContext db, ITenantAdminGuard guard, AuthOptions options, TimeProvider clock)
    : ITenantOnboardingService
{
    private readonly MultiTenancyOptions _tenancy = options.MultiTenancy;

    public async Task<MyMembershipDto> CreateAsync(Guid userId, CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var name = ValidName(request.Name);
        var now = clock.GetUtcNow();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = await UniqueSlugAsync(name, cancellationToken),
            TaxNo = NullIfEmpty(request.TaxNo),
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
            IsDefault = !await db.Set<TenantMembership>().HasDefaultAsync(userId, cancellationToken),
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        db.Add(tenant);
        db.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return new MyMembershipDto(membership.Id, TenantDto.From(tenant), membership.Status, membership.Roles, membership.IsDefault);
    }

    public async Task<TenantDto> UpdateAsync(Guid tenantId, TenantActor actor, UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var tenant = await db.Set<Tenant>().GetAsync(tenantId, cancellationToken);
        tenant.Name = ValidName(request.Name);
        tenant.TaxNo = NullIfEmpty(request.TaxNo);
        await db.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }

    public async Task<IReadOnlyList<TenantDto>> ListAsync(TenantStatus? status, CancellationToken cancellationToken)
        => await db.Set<Tenant>().AsNoTracking()
            .Where(t => status == null || t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantDto(t.Id, t.Name, t.Slug, t.TaxNo, t.Status, t.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<TenantDto> ReviewAsync(Guid tenantId, TenantStatus target, Guid reviewerId, string? note, CancellationToken cancellationToken)
    {
        var tenant = await db.Set<Tenant>().GetAsync(tenantId, cancellationToken);
        tenant.Review(target, reviewerId, note, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TenantDto.From(tenant);
    }

    private static string ValidName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        return name.Length is >= 2 and <= 200 ? name : throw IdentityException.Invalid("Şirket adı 2-200 karakter olmalı.");
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
