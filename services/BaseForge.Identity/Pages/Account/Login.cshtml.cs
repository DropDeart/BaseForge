using System.Security.Claims;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaseForge.Identity.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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

        // İlk kez: kullanıcıyı oluştur + dış girişi bağla.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? info.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(email))
        {
            Error = "Sağlayıcıdan e-posta alınamadı.";
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await _userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                Error = "Kullanıcı oluşturulamadı.";
                return Page();
            }
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(ReturnUrl);
    }
}
