using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>Token'a yazılacak aktif şirket ve şirket içi roller.</summary>
public sealed record ActiveTenant(Guid TenantId, string Name, IReadOnlyList<string> Roles);

/// <summary>Seçim sonucu: aktif şirket (ya da yok) veya istenen şirket geçersizse hata.</summary>
public sealed record TenantSelection(ActiveTenant? Tenant, string? Error);

/// <summary>Token isteğinde hangi şirketin aktif olacağını belirler.</summary>
public interface ITenantTokenResolver
{
    /// <summary>
    /// Sıra: istenen <c>tenant_id</c> parametresi → önceki token'daki şirket (refresh) → varsayılan üyelik → tek üyelik.
    /// </summary>
    Task<TenantSelection> ResolveAsync(Guid userId, string? requestedTenantId, string? previousTenantId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ITenantTokenResolver" />
internal sealed class TenantTokenResolver(IdentityServiceDbContext db, AuthOptions options) : ITenantTokenResolver
{
    private static readonly TenantSelection None = new(null, null);

    public async Task<TenantSelection> ResolveAsync(Guid userId, string? requestedTenantId, string? previousTenantId, CancellationToken cancellationToken)
    {
        if (!options.MultiTenancy.Enabled)
        {
            return None;
        }

        var memberships = await db.Set<TenantMembership>()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active && m.Tenant!.Status == TenantStatus.Active)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.TenantId, m.Tenant!.Name, m.Roles, m.IsDefault })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(requestedTenantId))
        {
            var requested = Guid.TryParse(requestedTenantId, out var id) ? memberships.Find(m => m.TenantId == id) : null;
            return requested is null
                ? new TenantSelection(null, "Bu şirketin aktif üyesi değilsiniz.")
                : new TenantSelection(new ActiveTenant(requested.TenantId, requested.Name, requested.Roles), null);
        }

        var chosen = (Guid.TryParse(previousTenantId, out var previous) ? memberships.Find(m => m.TenantId == previous) : null)
                     ?? memberships.Find(m => m.IsDefault)
                     ?? (memberships.Count == 1 ? memberships[0] : null);

        return chosen is null ? None : new TenantSelection(new ActiveTenant(chosen.TenantId, chosen.Name, chosen.Roles), null);
    }
}
