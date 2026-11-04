using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace BaseForge.Identity.Accounts;

/// <summary>Şifremi unuttum: e-postaya süreli bağlantı, bağlantıyla yeni şifre.</summary>
public interface IPasswordResetService
{
    /// <summary>Hesap varsa bağlantı gönderir; hesabın varlığı yanıttan anlaşılmaz.</summary>
    Task RequestAsync(string emailAddress, CancellationToken cancellationToken);

    /// <summary>Bağlantıdaki token ile şifreyi değiştirir; geçersizse 400.</summary>
    Task ResetAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken);
}

/// <summary>
/// HekimBurada'daki akışın genelleştirilmiş hali: aynı adrese bekleme süresi, ASP.NET Identity'nin süreli sıfırlama
/// token'ı; başarılı sıfırlamada e-posta doğrulanmış sayılır, kilit açılır ve eski oturumlar kapanır.
/// </summary>
internal sealed partial class PasswordResetService(
    UserManager<ApplicationUser> userManager,
    IEmailSender email,
    ISessionRevoker sessions,
    IMemoryCache cache,
    AuthOptions options,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const string InvalidLink = "Bağlantı geçersiz ya da süresi dolmuş. Yeni bir sıfırlama bağlantısı isteyin.";
    private readonly PasswordResetOptions _reset = options.PasswordReset;

    public async Task RequestAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var normalized = emailAddress?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw IdentityException.Invalid("E-posta gerekli.");
        }

        // Bekleme süresi ve "hesap yok" durumu aynı (sessiz) sonucu verir; hesabın varlığı sızmaz.
        var cacheKey = "pwreset:" + normalized.ToUpperInvariant();
        if (cache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        cache.Set(cacheKey, true, TimeSpan.FromSeconds(_reset.CooldownSeconds));
        var user = await userManager.FindByEmailAsync(normalized);
        if (user?.Email is not { Length: > 0 } address)
        {
            return;
        }

        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = $"{_reset.ResetUrl}{(_reset.ResetUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?')}userId={user.Id}&token={Uri.EscapeDataString(token)}";
            var greeting = string.IsNullOrWhiteSpace(user.FullName) ? "Merhaba," : $"Merhaba {user.FullName},";
            await email.SendAsync(EmailTemplate.Build(address, "Şifre sıfırlama", options.Messaging.Smtp.FromName, "Şifrenizi sıfırlayın",
                [greeting, $"Hesabınız için şifre sıfırlama talebi aldık. Bağlantı {_reset.TokenLifetimeHours} saat geçerlidir.",
                 "Bu talebi siz yapmadıysanız e-postayı yok sayın; şifreniz değişmez."],
                buttonText: "Şifremi sıfırla", buttonUrl: link), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Gönderim hatası kullanıcıya yansımaz (yanıt yine aynı); log'dan izlenir.
            LogSendFailed(logger, user.Id, ex);
        }
    }

    public async Task ResetAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            throw IdentityException.Invalid("Bağlantı geçersiz ya da eksik.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw IdentityException.Invalid(InvalidLink);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw IdentityException.Invalid(result.Errors.Any(e => e.Code == "InvalidToken")
                ? InvalidLink
                : string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Bağlantı e-postaya geldiği için adres sahipliği kanıtlandı; kilit açılır, eski oturumlar kapanır.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await sessions.RevokeAsync([user.Id], cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Şifre sıfırlama e-postası gönderilemedi (UserId: {UserId}).")]
    private static partial void LogSendFailed(ILogger logger, Guid userId, Exception exception);
}
