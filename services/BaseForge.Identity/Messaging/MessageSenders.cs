namespace BaseForge.Identity.Messaging;

/// <summary>SMS gönderici. Sağlayıcı (Netgsm, Vatan SMS vb.) bu arayüzle eklenir.</summary>
public interface ISmsSender
{
    /// <summary>E.164 numaraya metin gönderir.</summary>
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}

/// <summary>E-posta gönderici. Sağlayıcı (SMTP vb.) bu arayüzle eklenir.</summary>
public interface IEmailSender
{
    /// <summary>E-posta gönderir.</summary>
    Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>Geliştirme göndericisi: mesajı log'a yazar, dışarı bir şey göndermez.</summary>
public sealed partial class LogMessageSender(ILogger<LogMessageSender> logger) : ISmsSender, IEmailSender
{
    /// <inheritdoc />
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        LogSms(logger, phoneNumber, message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        LogEmail(logger, email, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[DEV SMS] {Phone}: {Message}")]
    private static partial void LogSms(ILogger logger, string phone, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[DEV E-POSTA] {Email} — {Subject}: {Body}")]
    private static partial void LogEmail(ILogger logger, string email, string subject, string body);
}
