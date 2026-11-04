using BaseForge.Identity.Configuration;
using BaseForge.Identity.Contacts;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Otp;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Accounts;

/// <summary>Telefonla giriş/kayıt ve hesabın telefon/e-posta doğrulaması.</summary>
public interface IContactVerificationService
{
    /// <summary>Giriş kodu gönderir (hesap olsun olmasın aynı yanıt; numara varlığı sızdırılmaz).</summary>
    Task<OtpSendResult> SendLoginCodeAsync(string phone, CancellationToken cancellationToken);

    /// <summary>Kodu doğrular; doğrulanmış telefonun sahibini döner ya da (kayıt açıksa) hesap açar.</summary>
    Task<ApplicationUser> SignInWithPhoneAsync(string phone, string code, string? requestedRole, CancellationToken cancellationToken);

    /// <summary>Hesaba eklenecek telefona kod gönderir.</summary>
    Task<OtpSendResult> SendPhoneCodeAsync(ApplicationUser user, string phone, CancellationToken cancellationToken);

    /// <summary>Telefonu doğrular ve hesaba yazar; bekleyen davetler bağlanır.</summary>
    Task ConfirmPhoneAsync(ApplicationUser user, string phone, string code, CancellationToken cancellationToken);

    /// <summary>Hesabın e-postasına kod gönderir.</summary>
    Task<OtpSendResult> SendEmailCodeAsync(ApplicationUser user, CancellationToken cancellationToken);

    /// <summary>E-postayı doğrular; bekleyen davetler bağlanır.</summary>
    Task ConfirmEmailAsync(ApplicationUser user, string code, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IContactVerificationService" />
internal sealed class ContactVerificationService(
    UserManager<ApplicationUser> userManager,
    IOtpService otp,
    IMembershipService memberships,
    AuthOptions options) : IContactVerificationService
{
    private const string InvalidCode = "Kod hatalı ya da süresi dolmuş.";

    public Task<OtpSendResult> SendLoginCodeAsync(string phone, CancellationToken cancellationToken)
        => otp.SendAsync(OtpChannel.Sms, NormalizePhone(phone), OtpPurpose.Login, cancellationToken);

    public async Task<ApplicationUser> SignInWithPhoneAsync(string phone, string code, string? requestedRole, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        if (!await otp.VerifyAsync(OtpChannel.Sms, normalized, OtpPurpose.Login, code, cancellationToken))
        {
            throw IdentityException.Invalid(InvalidCode);
        }

        var user = await FindByConfirmedPhoneAsync(normalized, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        if (!options.Registration.Enabled)
        {
            throw IdentityException.Forbidden("Bu numarayla kayıtlı bir hesap yok.");
        }

        user = new ApplicationUser { UserName = normalized, PhoneNumber = normalized, PhoneNumberConfirmed = true };
        EnsureSucceeded(await userManager.CreateAsync(user));
        await RegistrationRoles.AssignAsync(userManager, user, options.Registration, requestedRole);
        return user;
    }

    public async Task<OtpSendResult> SendPhoneCodeAsync(ApplicationUser user, string phone, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        await EnsurePhoneFreeAsync(user, normalized, cancellationToken);
        return await otp.SendAsync(OtpChannel.Sms, normalized, OtpPurpose.VerifyPhone, cancellationToken);
    }

    public async Task ConfirmPhoneAsync(ApplicationUser user, string phone, string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        if (!await otp.VerifyAsync(OtpChannel.Sms, normalized, OtpPurpose.VerifyPhone, code, cancellationToken))
        {
            throw IdentityException.Invalid(InvalidCode);
        }

        await EnsurePhoneFreeAsync(user, normalized, cancellationToken);
        user.PhoneNumber = normalized;
        user.PhoneNumberConfirmed = true;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await memberships.ClaimInvitationsAsync(user, cancellationToken);
    }

    public Task<OtpSendResult> SendEmailCodeAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var email = RequireEmail(user);
        if (user.EmailConfirmed)
        {
            throw IdentityException.Conflict("E-posta zaten doğrulanmış.");
        }

        return otp.SendAsync(OtpChannel.Email, email, OtpPurpose.VerifyEmail, cancellationToken);
    }

    public async Task ConfirmEmailAsync(ApplicationUser user, string code, CancellationToken cancellationToken)
    {
        var email = RequireEmail(user);
        if (!await otp.VerifyAsync(OtpChannel.Email, email, OtpPurpose.VerifyEmail, code, cancellationToken))
        {
            throw IdentityException.Invalid(InvalidCode);
        }

        user.EmailConfirmed = true;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        await memberships.ClaimInvitationsAsync(user, cancellationToken);
    }

    private string NormalizePhone(string phone) => ContactNormalizer.RequirePhone(phone, options.Otp.DefaultCountryCode);

    private static string RequireEmail(ApplicationUser user)
        => ContactNormalizer.Email(user.Email) ?? throw IdentityException.Invalid("Hesapta e-posta yok.");

    private Task<ApplicationUser?> FindByConfirmedPhoneAsync(string phone, CancellationToken cancellationToken)
        => userManager.Users.FindByConfirmedPhoneAsync(phone, cancellationToken);

    // Doğrulanmış bir numara tek hesaba aittir.
    private async Task EnsurePhoneFreeAsync(ApplicationUser user, string phone, CancellationToken cancellationToken)
    {
        var owner = await FindByConfirmedPhoneAsync(phone, cancellationToken);
        if (owner is not null && owner.Id != user.Id)
        {
            throw IdentityException.Conflict("Bu numara başka bir hesapta doğrulanmış.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw IdentityException.Invalid(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
    }
}
