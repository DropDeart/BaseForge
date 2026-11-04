using BaseForge.Identity.Configuration;

namespace BaseForge.Identity.Messaging;

/// <summary>E-posta/SMS gönderici kayıtları ve fail-fast kuralları.</summary>
public static class MessagingRegistration
{
    /// <summary>
    /// Sağlayıcıyı seçer. Production'da <c>Log</c> sağlayıcısıyla e-posta/SMS gerektiren bir özellik (OTP, şifre
    /// sıfırlama) açıksa uygulama açılmaz: kod/bağlantı hiçbir yere gitmez, sessizce çalışmamalı.
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, AuthOptions options, IHostEnvironment environment)
    {
        var messaging = options.Messaging;
        var log = MessagingOptions.LogProvider;
        var smtp = MessagingOptions.SmtpProvider;

        if (messaging.EmailProvider != log && messaging.EmailProvider != smtp)
        {
            throw new InvalidOperationException($"Auth:Messaging:EmailProvider '{messaging.EmailProvider}' desteklenmiyor ({log}, {smtp}).");
        }

        if (messaging.SmsProvider != log)
        {
            throw new InvalidOperationException($"Auth:Messaging:SmsProvider '{messaging.SmsProvider}' desteklenmiyor (şimdilik: {log}).");
        }

        if (messaging.EmailProvider == smtp && (string.IsNullOrWhiteSpace(messaging.Smtp.Host) || string.IsNullOrWhiteSpace(messaging.Smtp.FromAddress)))
        {
            throw new InvalidOperationException("Auth:Messaging:Smtp için Host ve FromAddress zorunlu.");
        }

        if (!environment.IsDevelopment())
        {
            var needsEmail = options.Otp.Enabled || options.PasswordReset.Enabled;
            if ((needsEmail && messaging.EmailProvider == log) || (options.Otp.Enabled && messaging.SmsProvider == log))
            {
                throw new InvalidOperationException(
                    "OTP veya şifre sıfırlama açık ama e-posta/SMS sağlayıcısı 'Log' (yalnız geliştirme). Gerçek sağlayıcı ayarlayın ya da özelliği kapatın.");
            }
        }

        services.AddSingleton<LogMessageSender>();
        services.AddSingleton<ISmsSender>(sp => sp.GetRequiredService<LogMessageSender>());
        if (messaging.EmailProvider == smtp)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<LogMessageSender>());
        }

        return services;
    }
}
