using System.Security.Claims;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>Ortak Giriş SPA'sının kullandığı e-posta/parola + dış sağlayıcı oturum + profil uçları.</summary>
[ApiController]
[Route("api/account")]
public sealed class AccountApiController : ControllerBase
{
    /// <summary>
    /// Profil uçları hem Ortak Giriş SPA'sının çerezini (cookie) hem de bağımsız SPA'ların
    /// ROPC ile aldığı Bearer access token'ını kabul eder — ikisi de aynı kullanıcıyı temsil eder.
    /// Değerler literal yazılır çünkü öznitelik argümanı derleme-zamanı sabiti olmalı
    /// (OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme const değil).
    /// </summary>
    private const string ProfileAuthSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore";

    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    private const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 MB

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public AccountApiController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = ProfileAuthSchemes)]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var hasPassword = await _userManager.HasPasswordAsync(user);
        return Ok(new MeResponse(user.Id, user.Email ?? user.UserName ?? string.Empty, user.FullName, user.AvatarUrl, hasPassword, roles));
    }

    [HttpPut("profile")]
    [Authorize(AuthenticationSchemes = ProfileAuthSchemes)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        await _userManager.UpdateAsync(user);
        return Ok();
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = ProfileAuthSchemes)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ErrorResponse("Yeni parola gerekli."));
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        var result = hasPassword
            ? await _userManager.ChangePasswordAsync(user, request.CurrentPassword ?? string.Empty, request.NewPassword)
            : await _userManager.AddPasswordAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(string.Join(" ", result.Errors.Select(e => e.Description))));
        }

        return Ok();
    }

    [HttpPost("avatar")]
    [Authorize(AuthenticationSchemes = ProfileAuthSchemes)]
    [RequestSizeLimit(MaxAvatarBytes + 4096)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse("Dosya gerekli."));
        }

        if (file.Length > MaxAvatarBytes)
        {
            return BadRequest(new ErrorResponse("Dosya en fazla 2 MB olabilir."));
        }

        if (!AllowedAvatarContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new ErrorResponse("Sadece JPEG, PNG, WEBP veya GIF yükleyebilirsiniz."));
        }

        var extension = file.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin",
        };

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{user.Id:N}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        // Eski yüklenmiş avatar'ı (varsa) temizle — dış sağlayıcı URL'lerine (Google vb.) dokunma.
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/", StringComparison.Ordinal))
        {
            var oldPath = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }
        }

        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        await _userManager.UpdateAsync(user);
        return Ok(new AvatarResponse(user.AvatarUrl));
    }

    [HttpGet("providers")]
    public async Task<IActionResult> Providers()
    {
        var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        return Ok(schemes.Select(s => s.Name));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("E-posta ve parola gerekli."));
        }

        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse("E-posta veya parola hatalı."));
        }

        return Ok();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Tüm alanları doldurun."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(string.Join(" ", result.Errors.Select(e => e.Description))));
        }

        await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return Ok();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }

    [HttpGet("external/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), values: new { returnUrl })
            ?? throw new InvalidOperationException("Callback URL çözümlenemedi.");
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-callback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string returnUrl = "/")
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return Redirect("/Account/Login?error=external");
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signIn.Succeeded)
        {
            return Redirect(returnUrl);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? info.Principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect("/Account/Login?error=external");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
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
            var created = await _userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                return Redirect("/Account/Login?error=external");
            }

            await _userManager.AddToRoleAsync(user, SeedData.UserRole);
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return Redirect(returnUrl);
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string? FullName, string Email, string Password);

public sealed record UpdateProfileRequest(string? FullName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string NewPassword);

public sealed record MeResponse(Guid Id, string Email, string? FullName, string? AvatarUrl, bool HasPassword, IEnumerable<string> Roles);

public sealed record AvatarResponse(string AvatarUrl);

public sealed record ErrorResponse(string Error);
