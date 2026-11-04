using System.Globalization;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Data;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Messaging;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Otp;

/// <summary>Gönderim sonucu: kod süresi ve yeniden gönderim beklemesi.</summary>
public sealed record OtpSendResult(int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>Tek kullanımlık kod gönderme ve doğrulama.</summary>
public interface IOtpService
{
    /// <summary>Kod üretir ve gönderir; bekleme/saatlik sınır aşılırsa 429.</summary>
    Task<OtpSendResult> SendAsync(OtpChannel channel, string target, OtpPurpose purpose, CancellationToken cancellationToken);

    /// <summary>Son kodu doğrular ve tüketir; yanlışsa deneme sayısını artırır.</summary>
    Task<bool> VerifyAsync(OtpChannel channel, string target, OtpPurpose purpose, string code, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IOtpService" />
internal sealed class OtpService(
    IdentityServiceDbContext db,
    ISmsSender sms,
    IEmailSender email,
    AuthOptions authOptions,
    TimeProvider clock) : IOtpService
{
    private readonly OtpOptions _options = authOptions.Otp;

    private DbSet<OtpChallenge> Challenges => db.Set<OtpChallenge>();

    public async Task<OtpSendResult> SendAsync(OtpChannel channel, string target, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var recent = await Challenges
            .Where(c => c.Target == target && c.Purpose == purpose && c.CreatedAt > now.AddHours(-1))
            .Select(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (recent.Count >= _options.MaxPerHour)
        {
            throw IdentityException.TooManyRequests("Çok fazla kod istendi; bir süre sonra tekrar deneyin.");
        }

        var wait = recent.Count == 0 ? 0 : (int)Math.Ceiling((recent.Max().AddSeconds(_options.ResendCooldownSeconds) - now).TotalSeconds);
        if (wait > 0)
        {
            throw IdentityException.TooManyRequests($"Yeni kod için {wait} saniye bekleyin.");
        }

        var code = OtpCode.Generate(_options.CodeLength);
        Challenges.Add(new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            Target = target,
            Purpose = purpose,
            CodeHash = OtpCode.Hash(code),
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(_options.LifetimeSeconds),
        });
        await db.SaveChangesAsync(cancellationToken);

        var minutes = Math.Max(1, _options.LifetimeSeconds / 60);
        if (channel == OtpChannel.Sms)
        {
            var text = string.Format(CultureInfo.InvariantCulture, "Doğrulama kodunuz: {0}. {1} dakika geçerlidir; kimseyle paylaşmayın.", code, minutes);
            await sms.SendAsync(target, text, cancellationToken);
        }
        else
        {
            await email.SendAsync(EmailTemplate.Build(target, "Doğrulama kodu", authOptions.Messaging.Smtp.FromName, "Doğrulama kodunuz",
                [$"Kod {minutes} dakika geçerlidir. Bu isteği siz yapmadıysanız e-postayı yok sayın; kodu kimseyle paylaşmayın."],
                highlight: code), cancellationToken);
        }

        return new OtpSendResult(_options.LifetimeSeconds, _options.ResendCooldownSeconds);
    }

    public async Task<bool> VerifyAsync(OtpChannel channel, string target, OtpPurpose purpose, string code, CancellationToken cancellationToken)
    {
        // Yalnız son gönderilen kod geçerlidir; eski kodlar yeni gönderimle hükümsüz kalır.
        var challenge = await Challenges
            .Where(c => c.Channel == channel && c.Target == target && c.Purpose == purpose)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = clock.GetUtcNow();
        if (challenge is null || !challenge.IsUsable(now, _options.MaxAttempts) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (!OtpCode.Matches(challenge.CodeHash, code))
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        challenge.ConsumedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
