using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>Bir <see cref="ServiceSpec"/>'ten mikroservis kod iskelesini üretir.</summary>
internal static class CodeGenerator
{
    /// <summary>Üretilen kodun referans verdiği BaseForge NuGet paket sürümü.</summary>
    private const string BaseForgeVersion = "0.1.0-alpha";

    public static IReadOnlyList<string> Generate(ServiceSpec spec, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var ns = NameUtil.Pascal(spec.Service);
        var contextName = ns + "DbContext";
        var written = new List<string>();

        var project = TemplateEngine.Render(
            Templates.Project,
            new ProjectFileModel { Namespace = ns, BaseForgeVersion = BaseForgeVersion });
        written.Add(WriteFile(Path.Combine(outputDir, ns + ".csproj"), project));

        foreach (var (name, entity) in spec.Entities)
        {
            var code = TemplateEngine.Render(Templates.Entity, BuildEntityModel(ns, name, entity));
            written.Add(WriteFile(Path.Combine(outputDir, "Entities", name + ".cs"), code));

            var feature = new FeatureFileModel { Namespace = ns, Name = name, Fields = BuildScalars(entity) };
            var featureDir = Path.Combine(outputDir, "Features", name + "s");
            written.Add(WriteFile(Path.Combine(featureDir, name + "Dto.cs"), TemplateEngine.Render(Templates.Dto, feature)));
            written.Add(WriteFile(Path.Combine(featureDir, name + "Commands.cs"), TemplateEngine.Render(Templates.Commands, feature)));
            written.Add(WriteFile(Path.Combine(featureDir, name + "Queries.cs"), TemplateEngine.Render(Templates.Queries, feature)));

            var controller = TemplateEngine.Render(Templates.Controller, new ControllerFileModel { Namespace = ns, Name = name });
            written.Add(WriteFile(Path.Combine(outputDir, "Controllers", name + "sController.cs"), controller));
        }

        // Program.cs + host dosyaları
        written.Add(WriteFile(
            Path.Combine(outputDir, "Program.cs"),
            TemplateEngine.Render(Templates.Program, new ProgramFileModel { Namespace = ns, ContextName = contextName })));

        var host = new HostFileModel { Namespace = ns, Service = spec.Service, Database = spec.Database };
        written.Add(WriteFile(Path.Combine(outputDir, "appsettings.json"), TemplateEngine.Render(Templates.AppSettings, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "Dockerfile"), TemplateEngine.Render(Templates.Dockerfile, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "docker-compose.snippet.yml"), TemplateEngine.Render(Templates.ComposeSnippet, host)));

        // gRPC client stub'ları (via: grpc olan dış referanslar için)
        foreach (var stub in CollectGrpcStubs(spec, ns))
        {
            written.Add(WriteFile(
                Path.Combine(outputDir, "Integration", stub.Entity + "Client.cs"),
                TemplateEngine.Render(Templates.GrpcStub, stub)));
        }

        var contextModel = new ContextFileModel
        {
            Namespace = ns,
            ServiceName = ns,
            ContextName = contextName,
            Entities = spec.Entities.Keys
                .Select(k => new EntityRef { Name = k, Plural = NameUtil.Pluralize(k) })
                .ToList(),
        };
        written.Add(WriteFile(
            Path.Combine(outputDir, "Data", contextName + ".cs"),
            TemplateEngine.Render(Templates.DbContext, contextModel)));

        return written;
    }

    private static EntityFileModel BuildEntityModel(string ns, string name, EntitySpec entity)
    {
        var model = new EntityFileModel
        {
            Namespace = ns,
            Name = name,
            Scalars = BuildScalars(entity),
        };

        foreach (var (relName, relation) in entity.Relations)
        {
            var kind = relation.Kind.ToUpperInvariant();
            var isCollection = kind is not ("MANY-TO-ONE" or "ONE-TO-ONE");
            model.Navigations.Add(new NavModel
            {
                Name = NameUtil.Pascal(relName),
                Type = relation.Target,
                IsCollection = isCollection,
            });
        }

        return model;
    }

    private static List<GrpcStubFileModel> CollectGrpcStubs(ServiceSpec spec, string ns)
    {
        var stubs = new Dictionary<string, GrpcStubFileModel>(StringComparer.Ordinal);
        foreach (var entity in spec.Entities.Values)
        {
            foreach (var externalRef in entity.ExternalRefs.Values)
            {
                if (!string.Equals(externalRef.Via, "grpc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = externalRef.Target;
                var entityName = target.Contains('/', StringComparison.Ordinal)
                    ? target[(target.LastIndexOf('/') + 1)..]
                    : target;
                entityName = NameUtil.Pascal(entityName);
                stubs[entityName] = new GrpcStubFileModel { Namespace = ns, Target = target, Entity = entityName };
            }
        }

        return [.. stubs.Values];
    }

    /// <summary>Entity'nin yazılabilir skaler alanları: props + (many/one-to-one) FK id + dış ref id.</summary>
    private static List<ScalarModel> BuildScalars(EntitySpec entity)
    {
        var scalars = new List<ScalarModel>();

        foreach (var (propName, propType) in entity.Props)
        {
            var csharp = TypeMap.ToCSharp(propType);
            scalars.Add(new ScalarModel
            {
                Name = NameUtil.Pascal(propName),
                Type = csharp,
                Init = string.Equals(csharp, "string", StringComparison.Ordinal) ? " = string.Empty;" : string.Empty,
            });
        }

        foreach (var (relName, relation) in entity.Relations)
        {
            var kind = relation.Kind.ToUpperInvariant();
            if (kind is "MANY-TO-ONE" or "ONE-TO-ONE")
            {
                scalars.Add(new ScalarModel { Name = NameUtil.Pascal(relName) + "Id", Type = "Guid" });
            }
        }

        foreach (var externalRef in entity.ExternalRefs.Values)
        {
            scalars.Add(new ScalarModel { Name = NameUtil.Pascal(externalRef.Store), Type = "Guid" });
        }

        return scalars;
    }

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
