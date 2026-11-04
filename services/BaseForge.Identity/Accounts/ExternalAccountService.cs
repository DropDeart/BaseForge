using System.Security.Claims;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Accounts;

/// <summary>Dış giriş sonucu: kullanıcı ya da giriş sayfasına dönecek hata kodu.</summary>
public sealed record ExternalAccountResult(ApplicationUser? User, string? Error)
{
    /// <summary>Başarılı.</summary>
    public static ExternalAccountResult Ok(ApplicationUser user) => new(user, null);

    /// <summary>Hata kodu (?error=...).</summary>
    public static ExternalAccountResult Fail(string error) => new(null, error);
}

/// <summary>Google/Apple/Facebook vb. ile ilk girişte hesabı bulur, bağlar ya da açar.</summary>
public interface IExternalAccountService
{
    /// <summary>Sağlayıcı bilgisinden kullanıcıyı çözer ve dış girişi bağlar.</summary>
    Task<ExternalAccountResult> ResolveAsync(ExternalLoginInfo info);
}

/// <summary>
/// Kurallar (API ve Razor giriş sayfası aynı yolu kullanır): kayıt kapalıyken hesap açılmaz; parolalı hesaba
/// e-posta eşleşmesiyle otomatik bağlanmaz (doğrulamayan sağlayıcı üzerinden hesap ele geçirilmesin).
/// </summary>
internal sealed class ExternalAccountService(UserManager<ApplicationUser> userManager, AuthOptions options) : IExternalAccountService
{
    /// <summary>Sağlayıcıdan e-posta alınamadı.</summary>
    public const string NoEmail = "external";

    /// <summary>Kayıt kapalı.</summary>
    public const string RegistrationClosed = "registration-closed";

    /// <summary>Aynı e-postayla parolalı hesap var.</summary>
    public const string AccountExists = "account-exists";

    public async Task<ExternalAccountResult> ResolveAsync(ExternalLoginInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? info.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(email))
        {
            return ExternalAccountResult.Fail(NoEmail);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            if (!options.Registration.Enabled)
            {
                return ExternalAccountResult.Fail(RegistrationClosed);
            }

            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name);
            var picture = info.Principal.FindFirstValue("picture");
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = string.IsNullOrWhiteSpace(displayName) || displayName == email ? null : displayName,
                AvatarUrl = string.IsNullOrWhiteSpace(picture) ? null : picture,
            };
            if (!(await userManager.CreateAsync(user)).Succeeded)
            {
                return ExternalAccountResult.Fail(NoEmail);
            }

            await RegistrationRoles.AssignAsync(userManager, user, options.Registration, requestedRole: null);
        }
        else if (await userManager.HasPasswordAsync(user))
        {
            return ExternalAccountResult.Fail(AccountExists);
        }

        await userManager.AddLoginAsync(user, info);
        return ExternalAccountResult.Ok(user);
    }
}
