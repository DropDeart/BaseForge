namespace BaseForge.CodeGen.Contracts;

/// <summary>Spec tip adlarını C# ve (görselleştirme için) veritabanı tiplerine eşler.</summary>
public static class TypeMap
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
            ["json"] = ("string", "jsonb"),
            // Gerçek C# tipi entity'ye özgüdür ({Entity}{Alan}, bkz. EnumTypeName); burada yalnızca servis sınırı
            // dışındaki (gRPC istemcisi vb.) temsili — değer adıyla string.
            ["enum"] = ("string", "text"),
        };

    /// <summary>UI dropdown'ı için desteklenen spec tip adları (kanonik sıra).</summary>
    public static IReadOnlyList<string> KnownTypes { get; } =
    [
        "string", "text", "int", "long", "short", "decimal",
        "double", "float", "bool", "datetime", "date", "guid", "uuid", "json", "enum",
    ];

    /// <summary>Spec tipi <c>enum</c> mu?</summary>
    public static bool IsEnum(string specType) => string.Equals(specType.Trim(), "enum", StringComparison.OrdinalIgnoreCase);

    /// <summary>Bir enum alanı için üretilen C# enum tipinin adı (örn. <c>Listing</c> + <c>Status</c> → <c>ListingStatus</c>).</summary>
    public static string EnumTypeName(string entityName, string propName) => NameUtil.Pascal(entityName) + NameUtil.Pascal(propName);

    /// <summary>Verilen spec tip adının bilinen (desteklenen) bir tip olup olmadığını döner.</summary>
    public static bool IsKnown(string specType) => Map.ContainsKey(specType.Trim());

    /// <summary>Spec tip adının karşılık geldiği C# tipini döner (bilinmiyorsa girdiyi olduğu gibi geri verir).</summary>
    public static string ToCSharp(string specType)
        => Map.TryGetValue(specType.Trim(), out var v) ? v.CSharp : specType.Trim();

    /// <summary>Spec tip adının görselleştirme (DB) tipini döner (bilinmiyorsa girdiyi olduğu gibi geri verir).</summary>
    public static string ToDisplay(string specType)
        => Map.TryGetValue(specType.Trim(), out var v) ? v.Display : specType.Trim();
}
