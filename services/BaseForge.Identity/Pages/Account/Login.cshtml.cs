using BaseForge.Identity.Accounts;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaseForge.Identity.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IExternalAccountService _externalAccounts;

    public LoginModel(SignInManager<ApplicationUser> signInManager, IExternalAccountService externalAccounts)
    {
        _externalAccounts = externalAccounts;
        _signInManager = signInManager;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";

    public string? Error { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        ExternalLogins = [.. await _signInManager.GetExternalAuthenticationSchemesAsync()];
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        ExternalLogins = [.. await _signInManager.GetExternalAuthenticationSchemesAsync()];

        var result = await _signInManager.PasswordSignInAsync(Email, Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        Error = "E-posta veya parola hatalı.";
        return Page();
    }

    public IActionResult OnPostExternal(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Page("/Account/Login", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        ExternalLogins = [.. await _signInManager.GetExternalAuthenticationSchemesAsync()];

        if (remoteError is not null)
        {
            Error = $"Sağlayıcı hatası: {remoteError}";
            return Page();
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            Error = "Dış giriş bilgisi alınamadı.";
            return Page();
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        // İlk kez: API ile aynı kurallarla hesabı bul/aç ve dış girişi bağla.
        var account = await _externalAccounts.ResolveAsync(info);
        if (account.User is null)
        {
            Error = account.Error switch
            {
                ExternalAccountService.RegistrationClosed => "Kayıt kapalı; bu hesapla giriş yapılamaz.",
                ExternalAccountService.AccountExists => "Bu e-postayla parolalı bir hesap var; parolanızla giriş yapın.",
                _ => "Sağlayıcıdan e-posta alınamadı.",
            };
            return Page();
        }

        await _signInManager.SignInAsync(account.User, isPersistent: false);
        return LocalRedirect(ReturnUrl);
    }
}
