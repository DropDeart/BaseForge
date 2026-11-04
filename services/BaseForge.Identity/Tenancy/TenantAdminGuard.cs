using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>İşlemi yapan: kullanıcı ve platform yöneticisi (onaylayan rol) mi.</summary>
public sealed record TenantActor(Guid UserId, bool IsPlatformAdmin);

/// <summary>Şirket yönetimi yetki ve tutarlılık kuralları.</summary>
public interface ITenantAdminGuard
{
    /// <summary>Şirket aktif ve işlemi yapan şirket yöneticisi (ya da platform yöneticisi) olmalı.</summary>
    Task EnsureCanManageAsync(Guid tenantId, TenantActor actor, CancellationToken cancellationToken);

    /// <summary>Roller şirket içi roller olmalı ve en az bir rol verilmeli.</summary>
    IReadOnlyList<string> ValidateRoles(IReadOnlyList<string>? roles);

    /// <summary>Şirketin son yöneticisi çıkarılamaz; <paramref name="newRoles"/> verilirse yönetici rolü düşürülemez.</summary>
    Task EnsureNotLastAdminAsync(TenantMembership membership, CancellationToken cancellationToken, IReadOnlyList<string>? newRoles = null);
}

/// <inheritdoc cref="ITenantAdminGuard" />
internal sealed class TenantAdminGuard(IdentityServiceDbContext db, AuthOptions options) : ITenantAdminGuard
{
    private readonly MultiTenancyOptions _tenancy = options.MultiTenancy;

    public async Task EnsureCanManageAsync(Guid tenantId, TenantActor actor, CancellationToken cancellationToken)
    {
        // Şirket durumu ve yöneticilik tek sorguda.
        var adminRole = _tenancy.AdminRole;
        var check = await db.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Status,
                IsAdmin = db.Set<TenantMembership>().Any(m => m.TenantId == tenantId && m.UserId == actor.UserId
                    && m.Status == MembershipStatus.Active && m.Roles.Contains(adminRole)),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw IdentityException.NotFound("Şirket bulunamadı.");

        if (!actor.IsPlatformAdmin && !check.IsAdmin)
        {
            throw IdentityException.Forbidden("Bu şirketin yöneticisi değilsiniz.");
        }

        if (check.Status != TenantStatus.Active)
        {
            throw IdentityException.Conflict("Şirket aktif değil (onay bekliyor ya da askıda).");
        }
    }

    public IReadOnlyList<string> ValidateRoles(IReadOnlyList<string>? roles)
    {
        var distinct = (roles ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
        {
            throw IdentityException.Invalid("En az bir rol seçilmeli.");
        }

        var unknown = distinct.Except(_tenancy.AllMemberRoles, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw IdentityException.Invalid($"Şirket içinde verilemeyen rol: {string.Join(", ", unknown)}.");
        }

        return distinct;
    }

    public async Task EnsureNotLastAdminAsync(TenantMembership membership, CancellationToken cancellationToken, IReadOnlyList<string>? newRoles = null)
    {
        var losesAdmin = newRoles is null || !newRoles.Contains(_tenancy.AdminRole, StringComparer.Ordinal);
        if (!losesAdmin || membership.Status != MembershipStatus.Active || !membership.Roles.Contains(_tenancy.AdminRole))
        {
            return;
        }

        var otherAdmins = await db.Set<TenantMembership>().AnyAsync(m =>
            m.TenantId == membership.TenantId && m.Id != membership.Id && m.Status == MembershipStatus.Active
            && m.Roles.Contains(_tenancy.AdminRole), cancellationToken);
        if (!otherAdmins)
        {
            throw IdentityException.Conflict("Şirketin son yöneticisi; önce başka bir yönetici atayın.");
        }
    }
}
