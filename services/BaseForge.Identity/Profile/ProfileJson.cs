using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Profile;

/// <summary>
/// Üretilen <see cref="UserProfile"/>'ın kullandığı şemadan bağımsız yardımcılar: JSON değerini spec tipine göre
/// doğrulayıp çevirme, claim biçimleme ve DDL çalıştırma (bkz. BaseForge docs/ARCH.md §6.3).
/// </summary>
public static class ProfileJson
{
    /// <summary>
    /// <paramref name="value"/>'yu <paramref name="type"/> (auth.yaml spec tipi) olarak okur. Başarılıysa
    /// <paramref name="result"/> kutulanmış değerdir (enum için <paramref name="enumType"/> tipinde; null yalnızca nullable'da).
    /// </summary>
    public static bool TryRead(JsonElement value, string type, bool nullable, int? maxLength, Type? enumType, out object? result, out string error)
    {
        result = null;
        error = string.Empty;

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (value.ValueKind == JsonValueKind.String && value.GetString()!.Length == 0 && type is not ("string" or "text")))
        {
            if (nullable)
            {
                return true;
            }

            error = "boş bırakılamaz.";
            return false;
        }

        switch (type)
        {
            case "string" or "text":
                if (value.ValueKind != JsonValueKind.String)
                {
                    error = "metin olmalı.";
                    return false;
                }

                var text = value.GetString()!.Trim();
                if (maxLength is { } max && text.Length > max)
                {
                    error = $"en fazla {max} karakter olabilir.";
                    return false;
                }

                result = text.Length == 0 && nullable ? null : text;
                return true;

            case "enum":
                if (value.ValueKind == JsonValueKind.String
                    && Enum.GetNames(enumType!).FirstOrDefault(n => n == value.GetString()) is { } name)
                {
                    result = Enum.Parse(enumType!, name);
                    return true;
                }

                error = $"geçersiz değer (izinli: {string.Join(", ", Enum.GetNames(enumType!))}).";
                return false;

            case "bool":
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    result = value.GetBoolean();
                    return true;
                }

                error = "true/false olmalı.";
                return false;

            case "int" or "long" or "short" or "decimal" or "double" or "float":
                var raw = value.ValueKind switch
                {
                    JsonValueKind.Number => value.GetRawText(),
                    JsonValueKind.String => value.GetString()!,
                    _ => null,
                };
                result = raw is null ? null : ParseNumber(type, raw);
                if (result is null)
                {
                    error = "geçerli bir sayı olmalı.";
                    return false;
                }

                return true;

            case "datetime":
                if (value.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
                {
                    result = dateTime.ToUniversalTime();
                    return true;
                }

                error = "geçerli bir tarih-saat olmalı (ISO 8601).";
                return false;

            case "date":
                if (value.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    result = date;
                    return true;
                }

                error = "geçerli bir tarih olmalı (yyyy-MM-dd).";
                return false;

            case "guid" or "uuid":
                if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var guid))
                {
                    result = guid;
                    return true;
                }

                error = "geçerli bir GUID olmalı.";
                return false;

            default:
                error = $"desteklenmeyen tip '{type}'.";
                return false;
        }
    }

    /// <summary>Bir profil değerini claim metnine çevirir (kültürden bağımsız); <see langword="null"/> → <see langword="null"/>.</summary>
    public static string? FormatClaim(object? value) => value switch
    {
        null => null,
        bool b => b ? "true" : "false",
        DateTimeOffset d => d.ToString("o", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>
    /// Parametresiz bir DDL komutu çalıştırır. <c>ExecuteSqlRaw</c> yerine: o, metni <c>string.Format</c>'tan geçirir
    /// ve bir string default'undaki süslü parantezleri bozar.
    /// </summary>
    public static async Task ExecuteDdlAsync(DbContext db, string sql, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static object? ParseNumber(string type, string raw)
    {
        const NumberStyles integer = NumberStyles.Integer;
        const NumberStyles real = NumberStyles.Float;
        var c = CultureInfo.InvariantCulture;
        return type switch
        {
            "int" => int.TryParse(raw, integer, c, out var i) ? i : null,
            "long" => long.TryParse(raw, integer, c, out var l) ? l : null,
            "short" => short.TryParse(raw, integer, c, out var s) ? s : null,
            "decimal" => decimal.TryParse(raw, real, c, out var m) ? m : null,
            "double" => double.TryParse(raw, real, c, out var d) && double.IsFinite(d) ? d : null,
            "float" => float.TryParse(raw, real, c, out var f) && float.IsFinite(f) ? f : null,
            _ => null,
        };
    }
}
