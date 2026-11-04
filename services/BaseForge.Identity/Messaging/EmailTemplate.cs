using System.Net;

namespace BaseForge.Identity.Messaging;

/// <summary>Sade, e-posta istemcileriyle uyumlu (tablo tabanlı, satır içi stil) ortak şablon.</summary>
public static class EmailTemplate
{
    /// <summary>Başlık, paragraflar, isteğe bağlı vurgulu kutu (kod) ve buton.</summary>
    /// <param name="productName">Gönderen ürün adı (Messaging:Smtp:FromName).</param>
    /// <param name="title">Başlık.</param>
    /// <param name="paragraphs">Düz metin paragraflar (HTML kodlanır).</param>
    /// <param name="highlight">Büyük gösterilecek değer (ör. doğrulama kodu).</param>
    /// <param name="buttonText">Buton metni.</param>
    /// <param name="buttonUrl">Buton bağlantısı (mutlak URL).</param>
    public static EmailMessage Build(
        string to, string subject, string productName, string title, IReadOnlyList<string> paragraphs,
        string? highlight = null, string? buttonText = null, string? buttonUrl = null)
    {
        static string e(string value) => WebUtility.HtmlEncode(value);
        var body = string.Concat(paragraphs.Select(p => $"""<p style="margin:0 0 14px;font-size:15px;line-height:22px;color:#334155">{e(p)}</p>"""));
        var code = highlight is null ? string.Empty
            : $"""<p style="margin:8px 0 18px;font-size:28px;letter-spacing:6px;font-weight:700;color:#0f172a">{e(highlight)}</p>""";
        var button = buttonText is null || buttonUrl is null ? string.Empty
            : $"""<p style="margin:8px 0 18px"><a href="{e(buttonUrl)}" style="display:inline-block;padding:12px 22px;border-radius:6px;background:#0f766e;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none">{e(buttonText)}</a></p>""";

        var html = $"""
            <!doctype html><html><body style="margin:0;padding:24px;background:#f1f5f9;font-family:Segoe UI,Arial,sans-serif">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center">
            <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:10px;padding:32px">
            <tr><td>
            <p style="margin:0 0 6px;font-size:12px;font-weight:700;letter-spacing:1px;color:#0f766e">{e(productName.ToUpperInvariant())}</p>
            <h1 style="margin:0 0 18px;font-size:22px;color:#0f172a">{e(title)}</h1>
            {body}{code}{button}
            </td></tr></table>
            </td></tr></table></body></html>
            """;

        var text = string.Join("\n\n", new[] { title }.Concat(paragraphs)
            .Append(highlight ?? string.Empty)
            .Append(buttonUrl is null ? string.Empty : $"{buttonText}: {buttonUrl}")
            .Where(s => s.Length > 0));

        return new EmailMessage(to, subject, html, text);
    }
}
