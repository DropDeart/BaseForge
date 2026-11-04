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
    [DbFact]
    public async Task Code_VerifiesOnce_AndIsPurposeBound()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var phone = TenancyFixture.Phone();

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
        var phone = TenancyFixture.Phone();

        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var tooSoon = await Assert.ThrowsAsync<IdentityException>(() => otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default));
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.Status);

        var code = fixture.Messages.LastCodeFor(phone);
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < fixture.Options.Otp.MaxAttempts; i++)
        {
            Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, wrong, default));
        }

        Assert.False(await otp.VerifyAsync(OtpChannel.Sms, phone, OtpPurpose.Login, code, default));   // deneme hakkı bitti
    }

    [DbFact]
    public async Task NewCode_InvalidatesOld()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>();
        var phone = TenancyFixture.Phone();

        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var first = fixture.Messages.LastCodeFor(phone);
        await TenancyFixture.AgeChallengesAsync(scope.ServiceProvider, phone);   // bekleme geçti
        await otp.SendAsync(OtpChannel.Sms, phone, OtpPurpose.Login, default);
        var second = fixture.Messages.LastCodeFor(phone);

        if (first != second)   // milyonda bir aynı kod üretilebilir; o durumda eski kodu ayrıca sınamak anlamsız
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
        var phone = TenancyFixture.Phone();
        var local = "0" + phone[3..];   // 0555... yazımı aynı numaraya gider

        await verification.SendLoginCodeAsync(local, default);
        var user = await verification.SignInWithPhoneAsync(local, fixture.Messages.LastCodeFor(phone), "Driver", default);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.Null(user.Email);
        Assert.Contains("Driver", await sp.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));

        await TenancyFixture.AgeChallengesAsync(sp, phone);
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
        var phone = TenancyFixture.Phone();

        var (tenantId, ownerId, _) = await TenancyFixture.ActiveTenantAsync(sp, "Telefon Davet");
        await sp.GetRequiredService<IMemberAdministrationService>().InviteAsync(tenantId, new TenantActor(ownerId, false),
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
        var (tenantId, ownerId, _) = await TenancyFixture.ActiveTenantAsync(sp, "E-posta Davet");
        await sp.GetRequiredService<IMemberAdministrationService>().InviteAsync(tenantId, new TenantActor(ownerId, false),
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

        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = TenancyFixture.Phone() })).Succeeded);
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email })).Succeeded);
        Assert.False((await users.CreateAsync(new ApplicationUser { UserName = "x" + email, Email = email })).Succeeded);
    }
}
