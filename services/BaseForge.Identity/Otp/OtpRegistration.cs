using System.Threading.RateLimiting;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Messaging;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaseForge.Identity.Otp;

/// <summary>OTP, gönderici ve hesap doğrulama kayıtları.</summary>
public static class OtpRegistration
{
    /// <summary>Telefonla giriş grant tipi (client grants: <c>otp</c>).</summary>
    public const string GrantType = "urn:baseforge:grant-type:otp";

    /// <summary>Cookie ve Bearer birlikte.</summary>
    public const string AuthSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore";

    /// <summary>Kod gönderme uçlarının IP başına sınırı.</summary>
    public const string SendPolicy = "otp.send";

    private const string LogProvider = "Log";

    /// <summary>
    /// Servisler. Sağlayıcı <c>Log</c> iken yalnız Development'ta çalışır: production'da kod hiçbir yere
    /// gitmeyeceği için uygulama açılmaz (fail-fast).
    /// </summary>
    public static IServiceCollection AddOtp(this IServiceCollection services, AuthOptions options, IHostEnvironment environment)
    {
        var otp = options.Otp;
        if (otp.Enabled && !environment.IsDevelopment()
            && (otp.SmsProvider == LogProvider || otp.EmailProvider == LogProvider))
        {
            throw new InvalidOperationException(
                "Auth:Otp açık ama SMS/e-posta sağlayıcısı 'Log' (yalnız geliştirme). Gerçek bir sağlayıcı ekleyin ya da Otp'yi kapatın.");
        }

        EnsureKnownProvider(otp.SmsProvider, "SmsProvider");
        EnsureKnownProvider(otp.EmailProvider, "EmailProvider");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<LogMessageSender>();
        services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<LogMessageSender>());
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<LogMessageSender>());
        services.AddScoped<IOtpService, OtpService>();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(SendPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    private static void EnsureKnownProvider(string provider, string name)
    {
        if (provider != LogProvider)
        {
            throw new InvalidOperationException($"Auth:Otp:{name} '{provider}' desteklenmiyor (şimdilik: {LogProvider}).");
        }
    }
}
