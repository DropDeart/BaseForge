using BaseForge.Identity.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket ve üyelik sorgularında tekrar eden kalıplar.</summary>
internal static class MembershipQueries
{
    /// <summary>Kullanıcının açık (davet, istek, üyelik) kayıtları.</summary>
    public static IQueryable<TenantMembership> OpenFor(this IQueryable<TenantMembership> memberships, Guid userId)
        => memberships.Where(m => m.UserId == userId && TenantMembership.OpenStatuses.Contains(m.Status));

    /// <summary>Kullanıcının varsayılan şirketi var mı? (ilk üyelik varsayılan olur)</summary>
    public static Task<bool> HasDefaultAsync(this IQueryable<TenantMembership> memberships, Guid? userId, CancellationToken cancellationToken)
        => memberships.AnyAsync(m => m.UserId == userId && m.IsDefault, cancellationToken);

    /// <summary>Şirket; yoksa 404.</summary>
    public static async Task<Tenant> GetAsync(this IQueryable<Tenant> tenants, Guid tenantId, CancellationToken cancellationToken)
        => await tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
           ?? throw IdentityException.NotFound("Şirket bulunamadı.");
}
