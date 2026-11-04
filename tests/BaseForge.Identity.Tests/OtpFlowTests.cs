using System.Net;
using BaseForge.Identity.Accounts;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Otp;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.Identity.Tests;

/// <summary>Kod gönderme/doğrulama, telefonla kayıt/giriş, telefon ve e-posta doğrulaması (gerçek Postgres).</summary>
public sealed class OtpFlowTests(TenancyFixture fixture) : IClassFixture<TenancyFixture>
{
    private static string Phone() => $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";

    [DbFact]
    public async Task Code_VerifiesOnce_AndIsPurposeBound()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var phone = Phone();

        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var code = fixture.Messages.LastCodeFor(phone);
        Assert.Equal(6, code.Length);

        Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.VerifyPhone, code, default));   // başka amaç
        Assert.True(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, code, default));
        Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, code, default));         // tek kullanımlık
    }

    [DbFact]
    public async Task Cooldown_AndAttemptLimit()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var phone = Phone();

        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var tooSoon = await Assert.ThrowsAsync<IdentityException>(() => otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default));
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.Status);

        var code = fixture.Messages.LastCodeFor(phone);
        for (var i = 0; i < fixture.Options.Otp.MaxAttempts; i++)
        {
            Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, "000000" == code ? "111111" : "000000", default));
        }

        Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, code, default));   // deneme hakkı bitti
    }

    [DbFact]
    public async Task NewCode_InvalidatesOld()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        var phone = Phone();

        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var first = fixture.Messages.LastCodeFor(phone);
        await db.Set<OtpChallenge>().Where(c => c.Target == phone)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.CreatedAt, c => c.CreatedAt.AddMinutes(-2)));   // bekleme geçti
        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var second = fixture.Messages.LastCodeFor(phone);

        if (first != second)
        {
            Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, first, default));
        }

        Assert.True(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, second, default));
    }

    [DbFact]
    public async Task PhoneSignIn_RegistersWithRole_ThenReturnsSameUser()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var verification = sp.GetRequiredService<IContactVerificationService>();
        var phone = Phone();
        var local = "0" + phone[3..];   // 0555... yazımı aynı numaraya gider

        await verification.SendLoginCodeAsync(local, default);
        var user = await verification.SignInWithPhoneAsync(local, fixture.Messages.LastCodeFor(phone), "Driver", default);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.Null(user.Email);
        Assert.Contains("Driver", await sp.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));

        await sp.GetRequiredService<IdentityServiceDbContext>().Set<OtpChallenge>().Where(c => c.Target == phone)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.CreatedAt, c => c.CreatedAt.AddMinutes(-2)));
        await verification.SendLoginCodeAsync(phone, default);
        var again = await verification.SignInWithPhoneAsync(phone, fixture.Messages.LastCodeFor(phone), null, default);
        Assert.Equal(user.Id, again.Id);

        await Assert.ThrowsAsync<IdentityException>(() => verification.SignInWithPhoneAsync(phone, "123", null, default));
    }

    [DbFact]
    public async Task VerifyPhone_ClaimsPhoneInvitation_AndPhoneIsUnique()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var verification = sp.GetRequiredService<IContactVerificationService>();
        var phone = Phone();

        var owner = await TenancyFixture.CreateUserAsync(sp, $"o{Guid.NewGuid():N}@t.local");
        var tenant = await sp.GetRequiredService<ITenantOnboardingService>().CreateAsync(owner.Id, new CreateTenantRequest("Telefon Davet", null), default);
        await sp.GetRequiredService<ITenantOnboardingService>().ReviewAsync(tenant.Tenant.Id, TenantStatus.Active, Guid.NewGuid(), null, default);
        await sp.GetRequiredService<IMemberAdministrationService>().InviteAsync(tenant.Tenant.Id, new TenantActor(owner.Id, false),
            new InviteMemberRequest(null, phone, ["Operator"]), default);

        var driver = await TenancyFixture.CreateUserAsync(sp, $"d{Guid.NewGuid():N}@t.local");
        await verification.SendPhoneCodeAsync(driver, phone, default);
        await verification.ConfirmPhoneAsync(driver, phone, fixture.Messages.LastCodeFor(phone), default);

        var invitation = Assert.Single(await sp.GetRequiredService<IMembershipService>().ListMineAsync(driver, default));
        Assert.Equal(MembershipStatus.Invited, invitation.Status);

        var other = await TenancyFixture.CreateUserAsync(sp, $"x{Guid.NewGuid():N}@t.local");
        var taken = await Assert.ThrowsAsync<IdentityException>(() => verification.SendPhoneCodeAsync(other, phone, default));
        Assert.Equal(HttpStatusCode.Conflict, taken.Status);
    }

    [DbFact]
    public async Task VerifyEmail_ConfirmsAndClaimsInvitation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var email = $"e{Guid.NewGuid():N}@t.local";
        var owner = await TenancyFixture.CreateUserAsync(sp, $"o{Guid.NewGuid():N}@t.local");
        var tenant = await sp.GetRequiredService<ITenantOnboardingService>().CreateAsync(owner.Id, new CreateTenantRequest("E-posta Davet", null), default);
        await sp.GetRequiredService<ITenantOnboardingService>().ReviewAsync(tenant.Tenant.Id, TenantStatus.Active, Guid.NewGuid(), null, default);
        await sp.GetRequiredService<IMemberAdministrationService>().InviteAsync(tenant.Tenant.Id, new TenantActor(owner.Id, false),
            new InviteMemberRequest(email, null, ["Operator"]), default);

        var user = await TenancyFixture.CreateUserAsync(sp, email, emailConfirmed: false);
        var verification = sp.GetRequiredService<IContactVerificationService>();
        await verification.SendEmailCodeAsync(user, default);
        await verification.ConfirmEmailAsync(user, fixture.Messages.LastCodeFor(email), default);

        Assert.True(user.EmailConfirmed);
        Assert.Single(await sp.GetRequiredService<IMembershipService>().ListMineAsync(user, default));
    }

    [DbFact]
    public async Task PhoneOnlyUser_AllowsMissingEmail_ButEmailStaysUnique()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"u{Guid.NewGuid():N}@t.local";

        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = Phone() })).Succeeded);
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email })).Succeeded);
        Assert.False((await users.CreateAsync(new ApplicationUser { UserName = "x" + email, Email = email })).Succeeded);
    }
}
