using System.Text.RegularExpressions;
using BaseForge.Core.Exceptions;

namespace BaseForge.Core.CQRS;

/// <summary>
/// İstemciden gelen <see cref="PagedRequest.SortBy"/> değerini güvenli, kanonik bir sıralama ifadesine çevirir.
/// Yalnızca <c>Alan</c>, <c>Alan asc</c> veya <c>Alan desc</c> parçaları (virgülle ayrılmış, en fazla
/// <see cref="MaxParts"/>) ve izin verilen alan adları kabul edilir; Dynamic LINQ'e serbest ifade ulaşmaz.
/// Bkz. docs/ARCH.md §6.2.
/// </summary>
public static partial class SortExpression
{
    /// <summary>Tek bir sıralamada izin verilen en fazla alan sayısı.</summary>
    public const int MaxParts = 5;

    /// <summary>
    /// <paramref name="sortBy"/>'ı doğrular ve <c>"Alan desc, Alan2"</c> biçiminde kanonik hale getirir.
    /// Boşsa <see langword="null"/> döner (çağıran varsayılan sıralamayı uygular).
    /// </summary>
    /// <param name="sortBy">İstemciden gelen ifade.</param>
    /// <param name="allowedFields">Sıralanabilir alan adları (büyük/küçük harf duyarsız eşleşir, kanonik adı döner).</param>
    /// <exception cref="ValidationException">Biçim geçersizse veya alan izinli değilse.</exception>
    public static string? Normalize(string? sortBy, IReadOnlyCollection<string> allowedFields)
    {
        ArgumentNullException.ThrowIfNull(allowedFields);
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var parts = sortBy.Split(',');
        if (parts.Length > MaxParts)
        {
            throw new ValidationException("sortBy", $"En fazla {MaxParts} alana göre sıralanabilir.");
        }

        var normalized = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var match = SortPart().Match(part);
            if (!match.Success)
            {
                throw new ValidationException("sortBy", $"Geçersiz sıralama ifadesi: '{part.Trim()}'. Biçim: 'Alan' veya 'Alan desc'.");
            }

            var field = allowedFields.FirstOrDefault(f => string.Equals(f, match.Groups["field"].Value, StringComparison.OrdinalIgnoreCase))
                ?? throw new ValidationException("sortBy", $"'{match.Groups["field"].Value}' alanına göre sıralanamaz. İzinli: {string.Join(", ", allowedFields)}.");
            var descending = string.Equals(match.Groups["dir"].Value, "desc", StringComparison.OrdinalIgnoreCase);
            normalized.Add(descending ? field + " desc" : field);
        }

        return string.Join(", ", normalized);
    }

    [GeneratedRegex(@"^\s*(?<field>[A-Za-z_][A-Za-z0-9_]*)(?:\s+(?<dir>asc|desc))?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SortPart();
}
