using BaseForge.Identity.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaseForge.Identity.Otp;

/// <summary>OTP, gönderici ve hesap doğrulama kayıtları.</summary>
public static class OtpRegistration
{
    /// <summary>Telefonla giriş grant tipi (client grants: <c>otp</c>).</summary>
    public const string GrantType = "urn:baseforge:grant-type:otp";

    /// <summary>Kod servisi ve eski kod temizliği (göndericiler: <see cref="Messaging.MessagingRegistration"/>).</summary>
    public static IServiceCollection AddOtp(this IServiceCollection services, AuthOptions options)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOtpService, OtpService>();
        if (options.Otp.Enabled)
        {
            services.AddHostedService<OtpCleanupService>();
        }

        return services;
    }
}
