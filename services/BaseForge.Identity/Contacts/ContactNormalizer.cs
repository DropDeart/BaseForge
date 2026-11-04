using BaseForge.Identity.ErrorHandling;

namespace BaseForge.Identity.Contacts;

/// <summary>E-posta ve telefonun tek biçimi; davet, OTP ve kullanıcı eşleşmesi aynı değeri kullanır.</summary>
public static class ContactNormalizer
{
    /// <summary>E-posta: kırpılmış, küçük harf; boşsa null.</summary>
    public static string? Email(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>
    /// Telefon E.164 (örn. +905551112233). "0555…" ve "555…" varsayılan ülke koduyla tamamlanır, "00" → "+".
    /// Geçersizse null.
    /// </summary>
    public static string? Phone(string? value, string defaultCountryCode = "+90")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        var country = new string(defaultCountryCode.Where(char.IsAsciiDigit).ToArray());

        var international = trimmed.StartsWith('+') ? digits
            : digits.StartsWith("00", StringComparison.Ordinal) ? digits[2..]
            : digits.StartsWith('0') ? country + digits[1..]
            : digits.Length == 10 ? country + digits
            : digits;

        return international.Length is >= 8 and <= 15 ? "+" + international : null;
    }

    /// <summary>Telefonu normalize eder; geçersizse 400.</summary>
    public static string RequirePhone(string? value, string defaultCountryCode)
        => Phone(value, defaultCountryCode) ?? throw IdentityException.Invalid("Telefon numarası geçersiz.");
}
