using System.Globalization;

namespace BaseForge.CodeGen.Spec;

/// <summary>
/// Spec tip adlarını proto3 skaler tiplerine ve C#↔proto dönüşüm ifadelerine eşler.
/// Protobuf'ta native <c>decimal</c>/<c>Guid</c>/<c>DateTimeOffset</c>/<c>DateOnly</c> karşılığı
/// olmadığından bunlar kayıpsız/basit olması için <c>string</c> (round-trip biçimli) taşınır.
/// </summary>
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
    public static string ToProtoExpr(string specType, string csharpAccess) => specType.Trim().ToLowerInvariant() switch
    {
        "decimal" => $"{csharpAccess}.ToString(CultureInfo.InvariantCulture)",
        "datetime" => $"{csharpAccess}.ToString(\"o\", CultureInfo.InvariantCulture)",
        "date" => $"{csharpAccess}.ToString(\"O\", CultureInfo.InvariantCulture)",
        "guid" or "uuid" => $"{csharpAccess}.ToString()",
        "short" => $"{csharpAccess}",
        _ => csharpAccess,
    };

    /// <summary>Proto mesaj alanından C# tipine dönüşüm ifadesi (örn. <c>decimal.Parse(response.Price, ...)</c>).</summary>
    public static string FromProtoExpr(string specType, string protoAccess) => specType.Trim().ToLowerInvariant() switch
    {
        "decimal" => $"decimal.Parse({protoAccess}, CultureInfo.InvariantCulture)",
        "datetime" => $"DateTimeOffset.Parse({protoAccess}, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)",
        "date" => $"DateOnly.ParseExact({protoAccess}, \"O\", CultureInfo.InvariantCulture)",
        "guid" or "uuid" => $"Guid.Parse({protoAccess})",
        "short" => $"(short){protoAccess}",
        _ => protoAccess,
    };
}
