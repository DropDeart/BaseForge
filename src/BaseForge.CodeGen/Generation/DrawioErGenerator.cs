using System.Globalization;
using System.Security;
using System.Text;
using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>Bir <see cref="ServiceSpec"/>'ten draw.io (diagrams.net) uyumlu ER diyagramı (.drawio XML) üretir.</summary>
internal static class DrawioErGenerator
{
    private const int ColumnSpacing = 300;
    private const int RowSpacing = 260;
    private const int BoxWidth = 230;
    private const int LineHeight = 18;
    private const int HeaderHeight = 34;

    public static string Generate(ServiceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var fkColumns = ComputeForeignKeyColumns(spec, out var edges);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"<mxfile host=\"BaseForge.CodeGen\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <diagram name=\"ER - {Xml(spec.Service)}\" id=\"er\">");
        sb.AppendLine("    <mxGraphModel dx=\"1000\" dy=\"700\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"1169\" pageHeight=\"826\" math=\"0\" shadow=\"0\">");
        sb.AppendLine("      <root>");
        sb.AppendLine("        <mxCell id=\"0\" />");
        sb.AppendLine("        <mxCell id=\"1\" parent=\"0\" />");

        var entityNames = spec.Entities.Keys.ToList();
        var columns = (int)Math.Ceiling(Math.Sqrt(entityNames.Count));
        if (columns == 0)
        {
            columns = 1;
        }

        var maxRow = 0;
        for (var i = 0; i < entityNames.Count; i++)
        {
            var name = entityNames[i];
            var entity = spec.Entities[name];
            var col = i % columns;
            var row = i / columns;
            maxRow = Math.Max(maxRow, row);
            var x = 40 + (col * ColumnSpacing);
            var y = 40 + (row * RowSpacing);

            var lines = BuildEntityLines(entity, fkColumns.GetValueOrDefault(name));
            AppendEntityCell(sb, name, lines, x, y);
        }

        // Dış servis referansları için kesik çizgili "stub" düğümleri.
        var externalTargets = CollectExternalTargets(spec);
        var stubY = 40 + ((maxRow + 1) * RowSpacing);
        var stubIndex = 0;
        foreach (var target in externalTargets)
        {
            var x = 40 + (stubIndex * ColumnSpacing);
            AppendExternalStub(sb, target, x, stubY);
            stubIndex++;
        }

        var edgeId = 0;
        foreach (var edge in edges)
        {
            AppendEdge(sb, $"e{edgeId}", $"n_{edge.From}", $"n_{edge.To}", dashed: false);
            edgeId++;
        }

        foreach (var (entityName, entity) in spec.Entities)
        {
            foreach (var externalRef in entity.ExternalRefs.Values)
            {
                AppendEdge(sb, $"e{edgeId}", $"n_{entityName}", $"x_{StubId(externalRef.Target)}", dashed: true);
                edgeId++;
            }
        }

        sb.AppendLine("      </root>");
        sb.AppendLine("    </mxGraphModel>");
        sb.AppendLine("  </diagram>");
        sb.AppendLine("</mxfile>");
        return sb.ToString();
    }

    private static List<string> BuildEntityLines(EntitySpec entity, List<string>? foreignKeys)
    {
        var lines = new List<string> { "Id : uuid (PK)" };

        foreach (var (propName, prop) in entity.Props)
        {
            var display = TypeMap.ToDisplay(prop.Type);
            if (prop.MaxLength is not null)
            {
                display += $"({prop.MaxLength})";
            }

            if (prop.Nullable)
            {
                display += "?";
            }

            lines.Add($"{propName} : {display}");
        }

        if (foreignKeys is not null)
        {
            lines.AddRange(foreignKeys);
        }

        foreach (var (_, externalRef) in entity.ExternalRefs)
        {
            lines.Add($"{externalRef.Store} : uuid (ext -> {externalRef.Target})");
        }

        // Audit alanları (BaseEntity)
        lines.Add("CreatedAt : timestamptz");
        lines.Add("IsDeleted : boolean");
        return lines;
    }

    private static void AppendEntityCell(StringBuilder sb, string name, List<string> lines, int x, int y)
    {
        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"<b>{Html(name)}</b><hr size=\"1\"/>");
        body.Append(string.Join("<br/>", lines.Select(Html)));

        var value = Xml(body.ToString());
        var height = HeaderHeight + (lines.Count * LineHeight);
        const string style = "rounded=0;whiteSpace=wrap;html=1;verticalAlign=top;align=left;spacingLeft=8;spacingRight=8;spacingTop=6;fillColor=#dae8fc;strokeColor=#6c8ebf;";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        <mxCell id=\"n_{Xml(name)}\" value=\"{value}\" style=\"{style}\" vertex=\"1\" parent=\"1\">");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"          <mxGeometry x=\"{x}\" y=\"{y}\" width=\"{BoxWidth}\" height=\"{height}\" as=\"geometry\" />");
        sb.AppendLine("        </mxCell>");
    }

    private static void AppendExternalStub(StringBuilder sb, string target, int x, int y)
    {
        var value = Xml($"<b>{Html(target)}</b><br/><i>(dış servis)</i>");
        const string style = "rounded=1;whiteSpace=wrap;html=1;dashed=1;fillColor=#f5f5f5;strokeColor=#999999;fontColor=#333333;";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        <mxCell id=\"x_{StubId(target)}\" value=\"{value}\" style=\"{style}\" vertex=\"1\" parent=\"1\">");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"          <mxGeometry x=\"{x}\" y=\"{y}\" width=\"{BoxWidth}\" height=\"50\" as=\"geometry\" />");
        sb.AppendLine("        </mxCell>");
    }

    private static void AppendEdge(StringBuilder sb, string id, string source, string target, bool dashed)
    {
        var style = dashed
            ? "endArrow=open;html=1;dashed=1;strokeColor=#999999;"
            : "endArrow=block;html=1;strokeColor=#6c8ebf;";

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"        <mxCell id=\"{id}\" style=\"{style}\" edge=\"1\" parent=\"1\" source=\"{Xml(source)}\" target=\"{Xml(target)}\">");
        sb.AppendLine("          <mxGeometry relative=\"1\" as=\"geometry\" />");
        sb.AppendLine("        </mxCell>");
    }

    private static Dictionary<string, List<string>> ComputeForeignKeyColumns(ServiceSpec spec, out List<(string From, string To)> edges)
    {
        var fkColumns = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var edgeSet = new HashSet<string>(StringComparer.Ordinal);
        edges = [];

        foreach (var (name, entity) in spec.Entities)
        {
            foreach (var relation in entity.Relations.Values)
            {
                var kind = relation.Kind.ToUpperInvariant();
                string holder;
                string principal;

                if (kind is "MANY-TO-ONE" or "ONE-TO-ONE")
                {
                    holder = name;
                    principal = relation.Target;
                }
                else
                {
                    // one-to-many: FK karşı (many) tarafta
                    holder = relation.Target;
                    principal = name;
                }

                AddForeignKey(fkColumns, holder, $"{principal}Id : uuid (FK -> {principal})");

                var edgeKey = $"{holder}->{principal}";
                if (edgeSet.Add(edgeKey))
                {
                    edges.Add((holder, principal));
                }
            }
        }

        return fkColumns;
    }

    private static void AddForeignKey(Dictionary<string, List<string>> map, string entity, string column)
    {
        if (!map.TryGetValue(entity, out var list))
        {
            list = [];
            map[entity] = list;
        }

        if (!list.Contains(column))
        {
            list.Add(column);
        }
    }

    private static List<string> CollectExternalTargets(ServiceSpec spec)
    {
        var targets = new List<string>();
        foreach (var entity in spec.Entities.Values)
        {
            foreach (var externalRef in entity.ExternalRefs.Values)
            {
                if (!targets.Contains(externalRef.Target, StringComparer.Ordinal))
                {
                    targets.Add(externalRef.Target);
                }
            }
        }

        return targets;
    }

    private static string StubId(string target)
    {
        var chars = target.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }

    private static string Html(string value) => SecurityElement.Escape(value) ?? value;

    private static string Xml(string value) => SecurityElement.Escape(value) ?? value;
}
