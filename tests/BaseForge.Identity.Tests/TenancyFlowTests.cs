using BaseForge.Identity.Data;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.Identity.Tests;

/// <summary>Şirket açma, onay, davet, katılma isteği ve token şirket seçimi (gerçek Postgres).</summary>
public sealed class TenancyFlowTests(TenancyFixture fixture) : IClassFixture<TenancyFixture>
{
    [DbFact]
    public async Task SelfServiceTenant_IsPendingUntilApproved()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var owner = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email());
        var onboarding = sp.GetRequiredService<ITenantOnboardingService>();
        var resolver = sp.GetRequiredService<ITenantTokenResolver>();

        var created = await onboarding.CreateAsync(owner.Id, new CreateTenantRequest("Öz Kantar A.Ş.", "1234567890"), default);
        Assert.Equal(TenantStatus.Pending, created.Tenant.Status);
        Assert.StartsWith("oz-kantar-a-s", created.Tenant.Slug, StringComparison.Ordinal);
        Assert.Null((await resolver.ResolveAsync(owner.Id, null, null, default)).Tenant);   // onaysız şirket token'a girmez

        await onboarding.ReviewAsync(created.Tenant.Id, TenantStatus.Active, Guid.NewGuid(), null, default);

        var active = (await resolver.ResolveAsync(owner.Id, null, null, default)).Tenant;
        Assert.NotNull(active);
        Assert.Equal(created.Tenant.Id, active.TenantId);
        Assert.Equal(["TenantAdmin"], active.Roles);
    }

    [DbFact]
    public async Task JoinRequest_ApprovedWithRole_AppearsInToken()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (tenantId, adminId, slug) = await TenancyFixture.ActiveTenantAsync(sp, "Katılım Ltd");
        var operatorUser = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email());
        var memberships = sp.GetRequiredService<IMembershipService>();
        var admin = new TenantActor(adminId, false);

        var request = await memberships.RequestJoinAsync(operatorUser.Id, slug, default);
        Assert.Equal(MembershipStatus.Requested, request.Status);
        await Assert.ThrowsAsync<IdentityException>(() => memberships.RequestJoinAsync(operatorUser.Id, slug, default));   // ikinci istek

        await sp.GetRequiredService<IMemberAdministrationService>().ApproveAsync(tenantId, admin, request.MembershipId, ["Operator"], default);

        var active = (await sp.GetRequiredService<ITenantTokenResolver>().ResolveAsync(operatorUser.Id, null, null, default)).Tenant;
        Assert.Equal(tenantId, active?.TenantId);
        Assert.Equal(["Operator"], active?.Roles);
    }

    [DbFact]
    public async Task InviteRegisteredUser_AcceptsAndBecomesMember()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (tenantId, adminId, _) = await TenancyFixture.ActiveTenantAsync(sp, "Davet AŞ");
        var invitee = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email());
        var admin = new TenantActor(adminId, false);

        var invitation = await sp.GetRequiredService<IMemberAdministrationService>()
            .InviteAsync(tenantId, admin, new InviteMemberRequest(invitee.Email!.ToUpperInvariant(), null, ["Operator"]), default);
        Assert.Equal(invitee.Id, invitation.UserId);
        Assert.Equal(MembershipStatus.Invited, invitation.Status);

        var memberships = sp.GetRequiredService<IMembershipService>();
        await memberships.AcceptAsync(invitee.Id, invitation.MembershipId, default);

        var mine = Assert.Single(await memberships.ListMineAsync(invitee, default));
        Assert.Equal(MembershipStatus.Active, mine.Status);
        Assert.True(mine.IsDefault);
    }

    [DbFact]
    public async Task InviteUnregisteredEmail_ClaimedOnlyAfterConfirmation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (tenantId, adminId, _) = await TenancyFixture.ActiveTenantAsync(sp, "Bekleyen Davet");
        var email = TenancyFixture.Email();

        await sp.GetRequiredService<IMemberAdministrationService>()
            .InviteAsync(tenantId, new TenantActor(adminId, false), new InviteMemberRequest(email, null, ["Operator"]), default);

        var memberships = sp.GetRequiredService<IMembershipService>();
        var unconfirmed = await TenancyFixture.CreateUserAsync(sp, email, emailConfirmed: false);
        Assert.Empty(await memberships.ListMineAsync(unconfirmed, default));   // doğrulanmamış e-posta daveti sahiplenemez

        unconfirmed.EmailConfirmed = true;
        var invitation = Assert.Single(await memberships.ListMineAsync(unconfirmed, default));
        Assert.Equal(MembershipStatus.Invited, invitation.Status);
    }

    [DbFact]
    public async Task InviteByPhone_WhenUserAlreadyRequested_ApprovesDirectly()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (tenantId, adminId, slug) = await TenancyFixture.ActiveTenantAsync(sp, "Telefon Nakliyat");
        var driver = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email(), phone: TenancyFixture.Phone());
        await sp.GetRequiredService<IMembershipService>().RequestJoinAsync(driver.Id, slug, default);

        var member = await sp.GetRequiredService<IMemberAdministrationService>()
            .InviteAsync(tenantId, new TenantActor(adminId, false), new InviteMemberRequest(null, driver.PhoneNumber, ["Operator"]), default);

        Assert.Equal(MembershipStatus.Active, member.Status);
    }

    [DbFact]
    public async Task Guards_NonAdminCannotInvite_LastAdminCannotLeave()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (tenantId, adminId, _) = await TenancyFixture.ActiveTenantAsync(sp, "Kural AŞ");
        var stranger = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email());
        var admin = sp.GetRequiredService<IMemberAdministrationService>();

        var forbidden = await Assert.ThrowsAsync<IdentityException>(() =>
            admin.InviteAsync(tenantId, new TenantActor(stranger.Id, false), new InviteMemberRequest(TenancyFixture.Email(), null, ["Operator"]), default));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbidden.Status);

        await Assert.ThrowsAsync<IdentityException>(() =>
            admin.InviteAsync(tenantId, new TenantActor(adminId, false), new InviteMemberRequest(TenancyFixture.Email(), null, ["SuperAdmin"]), default));

        var adminMembership = await sp.GetRequiredService<IdentityServiceDbContext>().Set<TenantMembership>()
            .SingleAsync(m => m.TenantId == tenantId && m.UserId == adminId);
        var lastAdmin = await Assert.ThrowsAsync<IdentityException>(() =>
            sp.GetRequiredService<IMembershipService>().LeaveAsync(adminId, adminMembership.Id, default));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, lastAdmin.Status);

        // Platform yöneticisi (onaylayan rol) üye olmadan da yönetebilir.
        await admin.ListAsync(tenantId, new TenantActor(stranger.Id, true), default);
    }

    [DbFact]
    public async Task MultiCompany_DefaultSwitchAndRefresh()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (firstId, firstAdmin, _) = await TenancyFixture.ActiveTenantAsync(sp, "Birinci Şirket");
        var (secondId, secondAdmin, _) = await TenancyFixture.ActiveTenantAsync(sp, "İkinci Şirket");
        var user = await TenancyFixture.CreateUserAsync(sp, TenancyFixture.Email());
        var admin = sp.GetRequiredService<IMemberAdministrationService>();
        var memberships = sp.GetRequiredService<IMembershipService>();
        var resolver = sp.GetRequiredService<ITenantTokenResolver>();

        foreach (var (tenantId, adminId) in new[] { (firstId, firstAdmin), (secondId, secondAdmin) })
        {
            var invite = await admin.InviteAsync(tenantId, new TenantActor(adminId, false), new InviteMemberRequest(user.Email, null, ["Operator"]), default);
            await memberships.AcceptAsync(user.Id, invite.MembershipId, default);
        }

        Assert.Equal(firstId, (await resolver.ResolveAsync(user.Id, null, null, default)).Tenant?.TenantId);          // ilk kabul edilen varsayılan
        Assert.Equal(secondId, (await resolver.ResolveAsync(user.Id, secondId.ToString(), null, default)).Tenant?.TenantId);   // geçiş
        Assert.Equal(secondId, (await resolver.ResolveAsync(user.Id, null, secondId.ToString(), default)).Tenant?.TenantId);   // refresh korur
        Assert.NotNull((await resolver.ResolveAsync(user.Id, Guid.NewGuid().ToString(), null, default)).Error);              // üye olmadığı şirket

        var second = (await memberships.ListMineAsync(user, default)).Single(m => m.Tenant.Id == secondId);
        await memberships.SetDefaultAsync(user.Id, second.MembershipId, default);
        Assert.Equal(secondId, (await resolver.ResolveAsync(user.Id, null, null, default)).Tenant?.TenantId);

        await sp.GetRequiredService<ITenantOnboardingService>().ReviewAsync(secondId, TenantStatus.Suspended, Guid.NewGuid(), "ödeme", default);
        Assert.Equal(firstId, (await resolver.ResolveAsync(user.Id, null, null, default)).Tenant?.TenantId);   // askıdaki düşer
    }

    [DbFact]
    public async Task EnsureTables_IsIdempotent_OnExistingDatabase()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();

        // 0.6.3'ten gelen identity_db: kiracı tabloları yok.
        await db.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "TenantMemberships"; DROP TABLE IF EXISTS "Tenants";""");

        await TenancyModel.EnsureTablesAsync(db);
        await TenancyModel.EnsureTablesAsync(db);

        var (tenantId, _, _) = await TenancyFixture.ActiveTenantAsync(scope.ServiceProvider, "Sonradan Eklenen");
        Assert.True(await db.Set<TenantMembership>().AnyAsync(m => m.TenantId == tenantId));
    }
}
