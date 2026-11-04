using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Otp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BaseForge.Identity.Controllers;

/// <summary>
/// Kod gönderme ve doğrulama. Telefonla giriş kodu doğrulaması token ucunda yapılır
/// (<c>grant_type=urn:baseforge:grant-type:otp</c>, <c>phone</c>, <c>code</c>).
/// </summary>
[ApiController]
[Route("api/account")]
[RequireFeature(AuthFeature.Otp)]
[EnableRateLimiting(OtpRegistration.SendPolicy)]
public sealed class VerificationApiController(UserManager<ApplicationUser> userManager, IContactVerificationService verification)
    : ControllerBase
{
    /// <summary>Telefonla giriş/kayıt kodu gönderir.</summary>
    [HttpPost("otp/send")]
    [AllowAnonymous]
    public Task<OtpSendResult> SendLoginCode(PhoneRequest request, CancellationToken cancellationToken)
        => verification.SendLoginCodeAsync(request.Phone, cancellationToken);

    /// <summary>Hesaba eklenecek telefona kod gönderir.</summary>
    [HttpPost("phone/send")]
    [Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
    public async Task<OtpSendResult> SendPhoneCode(PhoneRequest request, CancellationToken cancellationToken)
        => await verification.SendPhoneCodeAsync(await userManager.GetRequiredUserAsync(User), request.Phone, cancellationToken);

    /// <summary>Telefonu doğrular ve hesaba yazar.</summary>
    [HttpPost("phone/verify")]
    [Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
    public async Task<IActionResult> VerifyPhone(PhoneCodeRequest request, CancellationToken cancellationToken)
    {
        await verification.ConfirmPhoneAsync(await userManager.GetRequiredUserAsync(User), request.Phone, request.Code, cancellationToken);
        return NoContent();
    }

    /// <summary>Hesabın e-postasına doğrulama kodu gönderir.</summary>
    [HttpPost("email/send")]
    [Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
    public async Task<OtpSendResult> SendEmailCode(CancellationToken cancellationToken)
        => await verification.SendEmailCodeAsync(await userManager.GetRequiredUserAsync(User), cancellationToken);

    /// <summary>E-postayı doğrular.</summary>
    [HttpPost("email/verify")]
    [Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
    public async Task<IActionResult> VerifyEmail(CodeRequest request, CancellationToken cancellationToken)
    {
        await verification.ConfirmEmailAsync(await userManager.GetRequiredUserAsync(User), request.Code, cancellationToken);
        return NoContent();
    }
}

/// <summary>Telefon.</summary>
public sealed record PhoneRequest(string Phone);

/// <summary>Telefon ve kod.</summary>
public sealed record PhoneCodeRequest(string Phone, string Code);

/// <summary>Kod.</summary>
public sealed record CodeRequest(string Code);
