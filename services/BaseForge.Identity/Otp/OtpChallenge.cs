namespace BaseForge.Identity.Otp;

/// <summary>Gönderilmiş bir tek kullanımlık kod (kodun kendisi değil, tuzlu hash'i saklanır).</summary>
public sealed class OtpChallenge
{
    /// <summary>Kimlik.</summary>
    public Guid Id { get; set; }

    /// <summary>Gönderim kanalı.</summary>
    public OtpChannel Channel { get; set; }

    /// <summary>Normalize telefon (E.164) ya da e-posta.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Kodun amacı; başka amaçla kullanılamaz.</summary>
    public OtpPurpose Purpose { get; set; }

    /// <summary>"tuz:hash" (Base64).</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Gönderim zamanı.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Son geçerlilik.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Hatalı deneme sayısı.</summary>
    public int Attempts { get; set; }

    /// <summary>Doğru kodla kullanıldığı zaman.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Hâlâ denenebilir mi?</summary>
    public bool IsUsable(DateTimeOffset now, int maxAttempts) => ConsumedAt is null && ExpiresAt > now && Attempts < maxAttempts;
}

/// <summary>Kod kanalı.</summary>
public enum OtpChannel
{
    /// <summary>SMS.</summary>
    Sms,

    /// <summary>E-posta.</summary>
    Email,
}

/// <summary>Kodun amacı.</summary>
public enum OtpPurpose
{
    /// <summary>Telefonla giriş / kayıt.</summary>
    Login,

    /// <summary>Hesaba telefon ekleme.</summary>
    VerifyPhone,

    /// <summary>Hesabın e-postasını doğrulama.</summary>
    VerifyEmail,
}
