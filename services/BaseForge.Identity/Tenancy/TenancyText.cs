using System.Globalization;
using System.Text;

namespace BaseForge.Identity.Tenancy;

/// <summary>Şirket kodu (slug) üretimi.</summary>
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
}
