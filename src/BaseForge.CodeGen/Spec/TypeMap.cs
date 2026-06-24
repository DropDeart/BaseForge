namespace BaseForge.CodeGen.Spec;

/// <summary>Spec tip adlarını C# ve (görselleştirme için) veritabanı tiplerine eşler.</summary>
internal static class TypeMap
{
    private static readonly Dictionary<string, (string CSharp, string Display)> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = ("string", "text"),
            ["text"] = ("string", "text"),
            ["int"] = ("int", "integer"),
            ["long"] = ("long", "bigint"),
            ["short"] = ("short", "smallint"),
            ["decimal"] = ("decimal", "numeric"),
            ["double"] = ("double", "double precision"),
            ["float"] = ("float", "real"),
            ["bool"] = ("bool", "boolean"),
            ["datetime"] = ("DateTimeOffset", "timestamptz"),
            ["date"] = ("DateOnly", "date"),
            ["guid"] = ("Guid", "uuid"),
            ["uuid"] = ("Guid", "uuid"),
        };

    public static bool IsKnown(string specType) => Map.ContainsKey(specType.Trim());

    public static string ToCSharp(string specType)
        => Map.TryGetValue(specType.Trim(), out var v) ? v.CSharp : specType.Trim();

    public static string ToDisplay(string specType)
        => Map.TryGetValue(specType.Trim(), out var v) ? v.Display : specType.Trim();
}
