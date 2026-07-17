using System.Globalization;

namespace BaseForge.CodeGen.Contracts;

/// <summary>İsim dönüşümleri (PascalCase, camelCase, basit çoğullama).</summary>
public static class NameUtil
{
    /// <summary>Verilen adın ilk harfini büyütür (PascalCase).</summary>
    public static string Pascal(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    /// <summary>Verilen adın ilk harfini küçültür (camelCase).</summary>
    public static string Camel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToLower(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    /// <summary>Basit İngilizce çoğullama (tablo/DbSet adları için).</summary>
    public static string Pluralize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.EndsWith('y') && value.Length > 1 && !IsVowel(value[^2]))
        {
            return value[..^1] + "ies";
        }

        if (value.EndsWith('s') || value.EndsWith('x') || value.EndsWith('z') ||
            value.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return value + "es";
        }

        return value + "s";
    }

    private static bool IsVowel(char c) => "aeiouAEIOU".Contains(c, StringComparison.Ordinal);
}
