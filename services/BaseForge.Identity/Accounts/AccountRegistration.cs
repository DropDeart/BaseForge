using System.Threading.RateLimiting;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace BaseForge.Identity.Accounts;

/// <summary>Hesap servisleri: dış giriş, doğrulama, şifre sıfırlama, oturum iptali.</summary>
public static class AccountRegistration
{
    /// <summary>Kod/bağlantı gönderen uçların IP başına sınırı.</summary>
    public const string MessageSendPolicy = "account.send";

    /// <summary>Servis kayıtları.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, AuthOptions options)
    {
        // RequireUniqueEmail yerine: e-posta zorunlu değil, verildiyse benzersiz.
        services.AddScoped<IUserValidator<ApplicationUser>, OptionalEmailValidator>();
        services.AddScoped<IExternalAccountService, ExternalAccountService>();
        services.AddScoped<IContactVerificationService, ContactVerificationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<ISessionRevoker, SessionRevoker>();
        services.AddMemoryCache();

        // Şifre sıfırlama bağlantısının süresi (Identity'nin varsayılan token sağlayıcısı).
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(options.PasswordReset.TokenLifetimeHours));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(MessageSendPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }
}
