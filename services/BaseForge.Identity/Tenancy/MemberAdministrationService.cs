using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Contacts;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket yöneticisinin üye işlemleri: davet, istek onayı, rol, çıkarma.</summary>
public interface IMemberAdministrationService
{
    /// <summary>Üyeler, bekleyen davetler ve istekler.</summary>
    Task<IReadOnlyList<MemberDto>> ListAsync(Guid tenantId, TenantActor actor, CancellationToken cancellationToken);

    /// <summary>E-posta veya telefonla davet eder; kişi zaten istek attıysa doğrudan onaylanır.</summary>
    Task<MemberDto> InviteAsync(Guid tenantId, TenantActor actor, InviteMemberRequest request, CancellationToken cancellationToken);

    /// <summary>Katılma isteğini rollerle onaylar.</summary>
    Task ApproveAsync(Guid tenantId, TenantActor actor, Guid membershipId, IReadOnlyList<string> roles, CancellationToken cancellationToken);

    /// <summary>Katılma isteğini reddeder.</summary>
    Task RejectAsync(Guid tenantId, TenantActor actor, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Üyenin şirket içi rollerini değiştirir.</summary>
    Task ChangeRolesAsync(Guid tenantId, TenantActor actor, Guid membershipId, IReadOnlyList<string> roles, CancellationToken cancellationToken);

    /// <summary>Üyeyi çıkarır ya da daveti geri alır.</summary>
    Task RemoveAsync(Guid tenantId, TenantActor actor, Guid membershipId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IMemberAdministrationService" />
internal sealed class MemberAdministrationService(
    IdentityServiceDbContext db,
    UserManager<ApplicationUser> userManager,
    ITenantAdminGuard guard,
    AuthOptions options,
    TimeProvider clock) : IMemberAdministrationService
{
    private DbSet<TenantMembership> Memberships => db.Set<TenantMembership>();

    public async Task<IReadOnlyList<MemberDto>> ListAsync(Guid tenantId, TenantActor actor, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        return await (
            from m in Memberships
            where m.TenantId == tenantId && TenantMembership.OpenStatuses.Contains(m.Status)
            join u in db.Users on m.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby m.Status, m.CreatedAt
            select new MemberDto(m.Id, m.UserId, u != null ? u.Email : m.InvitedEmail, u != null ? u.PhoneNumber : m.InvitedPhone,
                u != null ? u.FullName : null, m.Status, m.Roles, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberDto> InviteAsync(Guid tenantId, TenantActor actor, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var roles = guard.ValidateRoles(request.Roles);
        var email = ContactNormalizer.Email(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : ContactNormalizer.RequirePhone(request.Phone, options.Otp.DefaultCountryCode);

        if ((email is null) == (phone is null))
        {
            throw IdentityException.Invalid("E-posta ya da telefondan yalnızca biri verilmeli.");
        }

        var user = email is not null
            ? await userManager.FindByEmailAsync(email)
            : await db.Users.FindByConfirmedPhoneAsync(phone!, cancellationToken);
        var now = clock.GetUtcNow();

        var existing = await Memberships.FirstOrDefaultAsync(m => m.TenantId == tenantId
            && TenantMembership.OpenStatuses.Contains(m.Status)
            && (user != null ? m.UserId == user.Id : (email != null ? m.InvitedEmail == email : m.InvitedPhone == phone)), cancellationToken);

        TenantMembership membership;
        if (existing is { Status: MembershipStatus.Requested })
        {
            existing.Approve(roles, now);   // kişi zaten katılmak istemiş: davet = onay
            membership = existing;
        }
        else if (existing is not null)
        {
            throw IdentityException.Conflict("Bu kişi şirkette zaten üye ya da davetli.");
        }
        else
        {
            membership = new TenantMembership
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user?.Id,
                InvitedEmail = user is null ? email : null,
                InvitedPhone = user is null ? phone : null,
                Roles = [.. roles],
                Status = MembershipStatus.Invited,
                CreatedAt = now,
                CreatedByUserId = actor.UserId,
            };
            Memberships.Add(membership);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new MemberDto(membership.Id, membership.UserId, user?.Email ?? email, user?.PhoneNumber ?? phone, user?.FullName,
            membership.Status, membership.Roles, membership.CreatedAt);
    }

    public async Task ApproveAsync(Guid tenantId, TenantActor actor, Guid membershipId, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var membership = await FindAsync(tenantId, membershipId, cancellationToken);
        membership.Approve(guard.ValidateRoles(roles), clock.GetUtcNow());
        membership.IsDefault = !await Memberships.HasDefaultAsync(membership.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid tenantId, TenantActor actor, Guid membershipId, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var membership = await FindAsync(tenantId, membershipId, cancellationToken);
        membership.Reject(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeRolesAsync(Guid tenantId, TenantActor actor, Guid membershipId, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var membership = await FindAsync(tenantId, membershipId, cancellationToken);
        if (membership.Status is not (MembershipStatus.Active or MembershipStatus.Invited))
        {
            throw IdentityException.Conflict("Yalnızca üyenin ya da bekleyen davetin rolleri değiştirilebilir.");
        }

        var validated = guard.ValidateRoles(roles);
        await guard.EnsureNotLastAdminAsync(membership, cancellationToken, validated);
        membership.Roles = [.. validated];
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid tenantId, TenantActor actor, Guid membershipId, CancellationToken cancellationToken)
    {
        await guard.EnsureCanManageAsync(tenantId, actor, cancellationToken);
        var membership = await FindAsync(tenantId, membershipId, cancellationToken);
        await guard.EnsureNotLastAdminAsync(membership, cancellationToken);
        membership.Remove(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TenantMembership> FindAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
        => await Memberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.TenantId == tenantId, cancellationToken)
           ?? throw IdentityException.NotFound("Üyelik bulunamadı.");
}
