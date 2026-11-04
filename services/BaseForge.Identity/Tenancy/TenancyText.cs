using System.Globalization;
using System.Text;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket kodu (slug) ve iletişim bilgisi normalizasyonu.</summary>
public static class TenancyText
{
    private const int MaxSlugLength = 50;

    /// <summary>Addan katılım kodu: "Öz Kantar A.Ş." → "oz-kantar-a-s".</summary>
    public static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")).Replace('ı', 'i').Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length > MaxSlugLength ? slug[..MaxSlugLength].TrimEnd('-') : slug;
    }

    /// <summary>E-posta: kırpılmış, küçük harf.</summary>
    public static string? Email(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>Telefon: yalnız rakamlar ve baştaki + (örn. +905551112233).</summary>
    public static string? Phone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? null : (value.TrimStart().StartsWith('+') ? "+" : string.Empty) + digits;
    }
}
