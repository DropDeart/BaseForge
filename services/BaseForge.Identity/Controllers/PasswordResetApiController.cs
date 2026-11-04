using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BaseForge.Identity.Controllers;

/// <summary>Şifremi unuttum ve şifre sıfırlama.</summary>
[ApiController]
[Route("api/account")]
[RequireFeature(AuthFeature.PasswordReset)]
[EnableRateLimiting(AccountRegistration.MessageSendPolicy)]
public sealed class PasswordResetApiController(IPasswordResetService passwordReset) : ControllerBase
{
    private const string GenericMessage =
        "Bu e-posta adresiyle kayıtlı bir hesap varsa şifre sıfırlama bağlantısı gönderildi. Gelen kutunuzu (ve spam klasörünü) kontrol edin.";

    /// <summary>Sıfırlama bağlantısı ister (her durumda aynı yanıt).</summary>
    [HttpPost("forgot-password")]
    public async Task<ForgotPasswordResponse> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordReset.RequestAsync(request.Email ?? string.Empty, cancellationToken);
        return new ForgotPasswordResponse(GenericMessage);
    }

    /// <summary>Bağlantıdaki token ile yeni şifre belirler.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordReset.ResetAsync(request.UserId, request.Token ?? string.Empty, request.NewPassword ?? string.Empty, cancellationToken);
        return NoContent();
    }
}

/// <summary>Şifremi unuttum isteği.</summary>
public sealed record ForgotPasswordRequest(string? Email);

/// <summary>Şifremi unuttum yanıtı.</summary>
public sealed record ForgotPasswordResponse(string Message);

/// <summary>Şifre sıfırlama isteği (bağlantıdaki userId ve token).</summary>
public sealed record ResetPasswordRequest(Guid UserId, string? Token, string? NewPassword);
