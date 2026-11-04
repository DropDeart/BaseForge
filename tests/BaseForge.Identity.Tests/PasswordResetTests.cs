using System.Text.RegularExpressions;
using BaseForge.Identity.Accounts;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.Identity.Tests;

/// <summary>Şifremi unuttum: bağlantı e-postası, sıfırlama, bekleme ve geçersiz bağlantı (gerçek Postgres).</summary>
public sealed class PasswordResetTests(TenancyFixture fixture) : IClassFixture<TenancyFixture>
{
    private static (Guid UserId, string Token) ParseLink(string text)
    {
        var match = Regex.Match(text, @"userId=([0-9a-f-]{36})&token=([^\s]+)");
        return (Guid.Parse(match.Groups[1].Value), Uri.UnescapeDataString(match.Groups[2].Value));
    }

    [DbFact]
    public async Task Request_SendsLink_AndResetChangesPassword()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = TenancyFixture.Email();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = false };
        Assert.True((await users.CreateAsync(user, "Eski!2345")).Succeeded);
        var reset = sp.GetRequiredService<IPasswordResetService>();

        await reset.RequestAsync(email.ToUpperInvariant(), default);
        var mail = fixture.Messages.LastTo(email);
        Assert.NotNull(mail);
        Assert.Contains("https://app.test/sifre-sifirla?userId=", mail, StringComparison.Ordinal);

        var (userId, token) = ParseLink(mail);
        await reset.ResetAsync(userId, token, "Yeni!2345", default);

        var updated = await users.FindByIdAsync(userId.ToString());
        Assert.True(await users.CheckPasswordAsync(updated!, "Yeni!2345"));
        Assert.True(updated!.EmailConfirmed);   // bağlantı e-postaya geldi: adres sahipliği kanıtlandı

        var reused = await Assert.ThrowsAsync<IdentityException>(() => reset.ResetAsync(userId, token, "Baska!2345", default));
        Assert.Contains("geçersiz", reused.Message, StringComparison.Ordinal);
    }

    [DbFact]
    public async Task UnknownEmail_AndCooldown_SendNothing()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var reset = sp.GetRequiredService<IPasswordResetService>();

        var unknown = TenancyFixture.Email();
        await reset.RequestAsync(unknown, default);   // hata yok, gönderim yok
        Assert.Null(fixture.Messages.LastTo(unknown));

        var email = TenancyFixture.Email();
        await TenancyFixture.CreateUserAsync(sp, email);
        await reset.RequestAsync(email, default);
        var first = fixture.Messages.LastTo(email);
        await reset.RequestAsync(email, default);      // bekleme süresi: ikinci e-posta gitmez
        Assert.Same(first, fixture.Messages.LastTo(email));
    }

    [DbFact]
    public async Task WeakPassword_IsRejectedWithReason()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var email = TenancyFixture.Email();
        await TenancyFixture.CreateUserAsync(sp, email);
        var reset = sp.GetRequiredService<IPasswordResetService>();
        await reset.RequestAsync(email, default);
        var (userId, token) = ParseLink(fixture.Messages.LastTo(email)!);

        await Assert.ThrowsAsync<IdentityException>(() => reset.ResetAsync(userId, token, "123", default));
    }
}
