using BaseForge.Identity.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BaseForge.Identity.Messaging;

/// <summary>
/// SMTP ile e-posta (MailKit). Resend, Google Workspace, Brevo, SES vb. aynı ayarla çalışır;
/// 465 doğrudan TLS, diğer portlar STARTTLS (sunucu destekliyorsa).
/// </summary>
public sealed class SmtpEmailSender(AuthOptions options) : IEmailSender
{
    private readonly SmtpOptions _smtp = options.Messaging.Smtp;

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_smtp.Username))
        {
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
