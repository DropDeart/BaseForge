using System.Globalization;

namespace BaseForge.CodeGen.Spec;

/// <summary>
/// Spec tip adlarını proto3 skaler tiplerine ve C#↔proto dönüşüm ifadelerine eşler.
/// Protobuf'ta native <c>decimal</c>/<c>Guid</c>/<c>DateTimeOffset</c>/<c>DateOnly</c> karşılığı
/// olmadığından bunlar kayıpsız/basit olması için <c>string</c> (round-trip biçimli) taşınır.
/// </summary>
/// <remarks>
/// Nullable alanlar için proto3 <c>optional</c> kullanılmaz (basit fallback): string/decimal/date/guid
/// için null → boş string, okurken boşsa null'a geri çevrilir (tam round-trip). int/long/short/double/
/// float/bool için null → tip varsayılanı (0/false) yazılır; okurken "null muydu yoksa gerçekten
/// varsayılan mıydı" ayrımı proto3'te taşınmadığından korunmaz (bilinen/dokümante edilen kısıt).
/// </remarks>
internal static class ProtoTypeMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "string",
        ["text"] = "string",
        ["int"] = "int32",
        ["long"] = "int64",
        ["short"] = "int32",
        ["decimal"] = "string",
        ["double"] = "double",
        ["float"] = "float",
        ["bool"] = "bool",
        ["datetime"] = "string",
        ["date"] = "string",
        ["guid"] = "string",
        ["uuid"] = "string",
    };

    public static string ToProto(string specType)
        => Map.TryGetValue(specType.Trim(), out var v) ? v : "string";

    /// <summary>C# alan erişiminden proto mesaj alanına atanacak ifade (örn. <c>value.Price.ToString(...)</c>).</summary>
    public static string ToProtoExpr(string specType, string csharpAccess, bool nullable = false)
    {
        var type = specType.Trim().ToLowerInvariant();
        if (!nullable)
        {
            return type switch
            {
                "decimal" => $"{csharpAccess}.ToString(CultureInfo.InvariantCulture)",
                "datetime" => $"{csharpAccess}.ToString(\"o\", CultureInfo.InvariantCulture)",
                "date" => $"{csharpAccess}.ToString(\"O\", CultureInfo.InvariantCulture)",
                "guid" or "uuid" => $"{csharpAccess}.ToString()",
                "short" => $"{csharpAccess}",
                _ => csharpAccess,
            };
        }

        return type switch
        {
            "decimal" => $"{csharpAccess}?.ToString(CultureInfo.InvariantCulture) ?? string.Empty",
            "datetime" => $"{csharpAccess}?.ToString(\"o\", CultureInfo.InvariantCulture) ?? string.Empty",
            "date" => $"{csharpAccess}?.ToString(\"O\", CultureInfo.InvariantCulture) ?? string.Empty",
            "guid" or "uuid" => $"{csharpAccess}?.ToString() ?? string.Empty",
            "string" or "text" => $"{csharpAccess} ?? string.Empty",
            "short" => $"(int){csharpAccess}.GetValueOrDefault()",
            _ => $"{csharpAccess}.GetValueOrDefault()",
        };
    }

    /// <summary>Proto mesaj alanından C# tipine dönüşüm ifadesi (örn. <c>decimal.Parse(response.Price, ...)</c>).</summary>
    public static string FromProtoExpr(string specType, string protoAccess, bool nullable = false)
    {
        var type = specType.Trim().ToLowerInvariant();
        if (!nullable)
        {
            return type switch
            {
                "decimal" => $"decimal.Parse({protoAccess}, CultureInfo.InvariantCulture)",
                "datetime" => $"DateTimeOffset.Parse({protoAccess}, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)",
                "date" => $"DateOnly.ParseExact({protoAccess}, \"O\", CultureInfo.InvariantCulture)",
                "guid" or "uuid" => $"Guid.Parse({protoAccess})",
                "short" => $"(short){protoAccess}",
                _ => protoAccess,
            };
        }

        // Sayısal/bool tiplerde null bilgisi proto3'te taşınmadığından non-nullable ile aynı ifade
        // kullanılır (implicit T -> T? dönüşümü ataması otomatik çalışır).
        return type switch
        {
            "decimal" => $"string.IsNullOrEmpty({protoAccess}) ? null : decimal.Parse({protoAccess}, CultureInfo.InvariantCulture)",
            "datetime" => $"string.IsNullOrEmpty({protoAccess}) ? null : DateTimeOffset.Parse({protoAccess}, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)",
            "date" => $"string.IsNullOrEmpty({protoAccess}) ? null : DateOnly.ParseExact({protoAccess}, \"O\", CultureInfo.InvariantCulture)",
            "guid" or "uuid" => $"string.IsNullOrEmpty({protoAccess}) ? null : Guid.Parse({protoAccess})",
            "string" or "text" => $"string.IsNullOrEmpty({protoAccess}) ? null : {protoAccess}",
            "short" => $"(short){protoAccess}",
            _ => protoAccess,
        };
    }
}
