using BaseForge.Identity.Configuration;
using BaseForge.Identity.Contacts;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Tenancy;

/// <summary>Kullanıcının kendi şirket ilişkileri: katılma, davet yanıtı, varsayılan şirket, ayrılma.</summary>
public interface IMembershipService
{
    /// <summary>Üyelikler, bekleyen davetler ve istekler.</summary>
    Task<IReadOnlyList<MyMembershipDto>> ListMineAsync(ApplicationUser user, CancellationToken cancellationToken);

    /// <summary>Şirket kodundan ad (katılmadan önce doğrulama).</summary>
    Task<TenantLookupDto> LookupAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Şirkete katılma isteği gönderir.</summary>
    Task<MyMembershipDto> RequestJoinAsync(Guid userId, string slug, CancellationToken cancellationToken);

    /// <summary>Daveti kabul eder.</summary>
    Task AcceptAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Daveti reddeder.</summary>
    Task DeclineAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Girişte seçilecek varsayılan şirketi belirler.</summary>
    Task SetDefaultAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Şirketten ayrılır ya da isteğini geri çeker.</summary>
    Task LeaveAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Doğrulanmış e-posta/telefona gelmiş davetleri kullanıcıya bağlar.</summary>
    Task ClaimInvitationsAsync(ApplicationUser user, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IMembershipService" />
internal sealed class MembershipService(IdentityServiceDbContext db, ITenantAdminGuard guard, AuthOptions options, TimeProvider clock)
    : IMembershipService
{
    private DbSet<TenantMembership> Memberships => db.Set<TenantMembership>();

    public async Task<IReadOnlyList<MyMembershipDto>> ListMineAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await ClaimInvitationsAsync(user, cancellationToken);
        var rows = await Memberships
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id
                        && (m.Status == MembershipStatus.Invited || m.Status == MembershipStatus.Requested || m.Status == MembershipStatus.Active))
            .OrderBy(m => m.Tenant!.Name)
            .ToListAsync(cancellationToken);
        return rows.Select(m => new MyMembershipDto(m.Id, TenantDto.From(m.Tenant!), m.Status, m.Roles, m.IsDefault)).ToList();
    }

    public async Task<TenantLookupDto> LookupAsync(string slug, CancellationToken cancellationToken)
    {
        var tenant = await FindActiveBySlugAsync(slug, cancellationToken);
        return new TenantLookupDto(tenant.Name, tenant.Slug);
    }

    public async Task<MyMembershipDto> RequestJoinAsync(Guid userId, string slug, CancellationToken cancellationToken)
    {
        var tenant = await FindActiveBySlugAsync(slug, cancellationToken);
        if (await Memberships.AnyAsync(m => m.TenantId == tenant.Id && m.UserId == userId
                && (m.Status == MembershipStatus.Invited || m.Status == MembershipStatus.Requested || m.Status == MembershipStatus.Active), cancellationToken))
        {
            throw IdentityException.Conflict("Bu şirkette zaten üyeliğiniz, davetiniz ya da bekleyen isteğiniz var.");
        }

        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = userId,
            Status = MembershipStatus.Requested,
            CreatedAt = clock.GetUtcNow(),
            CreatedByUserId = userId,
        };
        Memberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return new MyMembershipDto(membership.Id, TenantDto.From(tenant), membership.Status, membership.Roles, false);
    }

    public async Task AcceptAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await FindMineAsync(userId, membershipId, cancellationToken);
        if (membership.Tenant!.Status != TenantStatus.Active)
        {
            throw IdentityException.Conflict("Şirket aktif değil; davet şu an kabul edilemez.");
        }

        membership.Accept(clock.GetUtcNow());
        membership.IsDefault = !await Memberships.AnyAsync(m => m.UserId == userId && m.IsDefault, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeclineAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await FindMineAsync(userId, membershipId, cancellationToken);
        membership.Decline(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await FindMineAsync(userId, membershipId, cancellationToken);
        if (membership.Status != MembershipStatus.Active)
        {
            throw IdentityException.Conflict("Yalnızca üyesi olduğunuz şirket varsayılan yapılabilir.");
        }

        await Memberships.Where(m => m.UserId == userId && m.IsDefault)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.IsDefault, false), cancellationToken);
        membership.IsDefault = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await FindMineAsync(userId, membershipId, cancellationToken);
        await guard.EnsureNotLastAdminAsync(membership, cancellationToken);
        membership.Remove(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClaimInvitationsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Yalnız doğrulanmış iletişim bilgisi: başkasının e-postasıyla kayıt olup daveti sahiplenmek engellenir.
        var email = user.EmailConfirmed ? ContactNormalizer.Email(user.Email) : null;
        var phone = user.PhoneNumberConfirmed ? ContactNormalizer.Phone(user.PhoneNumber, options.Otp.DefaultCountryCode) : null;
        if (email is null && phone is null)
        {
            return;
        }

        var invitations = await Memberships
            .Where(m => m.UserId == null && m.Status == MembershipStatus.Invited
                        && ((email != null && m.InvitedEmail == email) || (phone != null && m.InvitedPhone == phone)))
            .ToListAsync(cancellationToken);
        if (invitations.Count == 0)
        {
            return;
        }

        var openTenants = await Memberships
            .Where(m => m.UserId == user.Id
                        && (m.Status == MembershipStatus.Invited || m.Status == MembershipStatus.Requested || m.Status == MembershipStatus.Active))
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var invitation in invitations)
        {
            if (openTenants.Contains(invitation.TenantId))
            {
                invitation.Remove(clock.GetUtcNow());   // aynı şirkette zaten kaydı var
                continue;
            }

            invitation.UserId = user.Id;
            openTenants.Add(invitation.TenantId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenant> FindActiveBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        var normalized = slug?.Trim().ToLowerInvariant() ?? string.Empty;
        return await db.Set<Tenant>().FirstOrDefaultAsync(t => t.Slug == normalized && t.Status == TenantStatus.Active, cancellationToken)
            ?? throw IdentityException.NotFound("Bu kodla aktif bir şirket bulunamadı.");
    }

    // Başkasının kaydı 404 (varlığı sızdırılmaz).
    private async Task<TenantMembership> FindMineAsync(Guid userId, Guid membershipId, CancellationToken cancellationToken)
        => await Memberships.Include(m => m.Tenant)
               .FirstOrDefaultAsync(m => m.Id == membershipId && m.UserId == userId, cancellationToken)
           ?? throw IdentityException.NotFound("Üyelik bulunamadı.");
}
