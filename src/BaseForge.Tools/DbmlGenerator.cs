using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BaseForge.Tools;

/// <summary>
/// Bir EF Core model'inden <see href="https://dbdiagram.io">dbdiagram.io</see> uyumlu
/// DBML metni üretir. Gerçek tablo/kolon adlarını, kolon tiplerini, birincil anahtarları,
/// null-edilebilirliği ve yabancı anahtar (FK) ilişkilerini yansıtır.
/// </summary>
public static class DbmlGenerator
{
    /// <summary>Verilen <see cref="DbContext"/>'in model'inden DBML üretir.</summary>
    /// <param name="context">Model'i okunacak EF Core context'i.</param>
    /// <returns>DBML metni.</returns>
    public static string Generate(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Generate(context.Model);
    }

    /// <summary>Verilen EF Core <see cref="IModel"/>'inden DBML üretir.</summary>
    /// <param name="model">Okunacak EF Core model'i.</param>
    /// <returns>DBML metni.</returns>
    public static string Generate(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var tables = new StringBuilder();
        var references = new StringBuilder();

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned())
            {
                continue;
            }

            var table = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            if (table is null)
            {
                continue;
            }

            AppendTable(tables, entityType, table.Value);
            AppendForeignKeys(references, entityType);
        }

        var sb = new StringBuilder();
        sb.AppendLine("// BaseForge.Tools ile EF Core model'inden üretildi (DBML).");
        sb.AppendLine("// Görselleştirmek için içeriği https://dbdiagram.io adresine yapıştırın.");
        sb.AppendLine();
        sb.Append(tables);
        if (references.Length > 0)
        {
            sb.Append(references);
        }

        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, IEntityType entityType, StoreObjectIdentifier table)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"Table {FormatTable(table)} {{");

        foreach (var property in entityType.GetProperties())
        {
            var column = property.GetColumnName(table) ?? property.Name;
            var columnType = property.GetColumnType(table) ?? property.ClrType.Name;

            var attributes = new List<string>();
            if (property.IsPrimaryKey())
            {
                attributes.Add("pk");
            }

            if (!property.IsNullable)
            {
                attributes.Add("not null");
            }

            var suffix = attributes.Count > 0
                ? $" [{string.Join(", ", attributes)}]"
                : string.Empty;

            sb.AppendLine(CultureInfo.InvariantCulture, $"  {Quote(column)} {Quote(columnType)}{suffix}");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendForeignKeys(StringBuilder sb, IEntityType entityType)
    {
        var dependent = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
        if (dependent is null)
        {
            return;
        }

        foreach (var foreignKey in entityType.GetForeignKeys())
        {
            var principal = StoreObjectIdentifier.Create(foreignKey.PrincipalEntityType, StoreObjectType.Table);
            if (principal is null)
            {
                continue;
            }

            var dependentColumns = ColumnList(foreignKey.Properties, dependent.Value);
            var principalColumns = ColumnList(foreignKey.PrincipalKey.Properties, principal.Value);

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"Ref: {FormatColumns(dependent.Value, dependentColumns)} > {FormatColumns(principal.Value, principalColumns)}");
        }
    }

    private static List<string> ColumnList(IReadOnlyList<IProperty> properties, StoreObjectIdentifier table)
    {
        var columns = new List<string>(properties.Count);
        foreach (var property in properties)
        {
            columns.Add(property.GetColumnName(table) ?? property.Name);
        }

        return columns;
    }

    private static string FormatTable(StoreObjectIdentifier table)
        => table.Schema is null ? Quote(table.Name) : $"{Quote(table.Schema)}.{Quote(table.Name)}";

    private static string FormatColumns(StoreObjectIdentifier table, List<string> columns)
    {
        var quoted = columns.ConvertAll(Quote);
        var columnPart = quoted.Count == 1 ? quoted[0] : $"({string.Join(", ", quoted)})";
        return $"{FormatTable(table)}.{columnPart}";
    }

    private static string Quote(string identifier) => $"\"{identifier}\"";
}
