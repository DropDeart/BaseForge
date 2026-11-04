namespace BaseForge.Core.CQRS;

/// <summary>
/// Serbest metin aramayı (<see cref="PagedRequest.Search"/>) güvenli bir SQL <c>LIKE/ILIKE</c> desenine çevirir:
/// kullanıcının yazdığı <c>%</c>, <c>_</c> ve <c>\</c> karakterleri <see cref="EscapeCharacter"/> ile kaçışlanır,
/// böylece joker olarak değil düz metin olarak aranır. Bkz. docs/ARCH.md §6.2.
/// </summary>
public static class LikePattern
{
    /// <summary>Desen içinde kullanılan kaçış karakteri (<c>EF.Functions.ILike(..., pattern, "\\")</c>).</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Aramanın uygulanması için gereken en kısa (kırpılmış) metin uzunluğu.</summary>
    public const int MinSearchLength = 2;

    /// <summary>
    /// "İçerir" deseni (<c>%metin%</c>) üretir. Metin boşsa veya <see cref="MinSearchLength"/>'ten kısaysa
    /// <see langword="null"/> döner — çağıran arama filtresini uygulamaz.
    /// </summary>
    public static string? ContainsOrNull(string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term) || term.Length < MinSearchLength)
        {
            return null;
        }

        return "%" + Escape(term) + "%";
    }

    /// <summary>LIKE joker karakterlerini (<c>%</c>, <c>_</c>) ve kaçış karakterinin kendisini kaçışlar.</summary>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }
}
