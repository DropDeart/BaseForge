using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>Bir <see cref="ServiceSpec"/>'ten mikroservis kod iskelesini üretir.</summary>
internal static class CodeGenerator
{
    /// <summary>Üretilen kodun referans verdiği BaseForge NuGet paket sürümü.</summary>
    private const string BaseForgeVersion = "0.2.1-alpha";

    /// <summary>Identity'nin gömülü <c>user.proto</c> kaynağındaki referans namespace'i.</summary>
    private const string IdentityReferenceNamespace = "BaseForge.Identity";

    public static IReadOnlyList<string> Generate(ServiceSpec spec, string outputDir, string? specPath = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var ns = NameUtil.Pascal(spec.Service);
        var contextName = ns + "DbContext";
        var written = new List<string>();

        // Dış referansları (via: grpc) önceden çözümle — Project/Program/AppSettings render'ları buna bağlı.
        var externalRefResolutions = ResolveExternalRefs(spec, ns, specPath);
        var richResolutions = externalRefResolutions.Where(r => r.IsRich).ToList();
        var grpcServerEntities = new List<string>();

        var project = TemplateEngine.Render(
            Templates.Project,
            new ProjectFileModel
            {
                Namespace = ns,
                BaseForgeVersion = BaseForgeVersion,
                ServerProtoFiles = spec.Entities.Keys.Select(k => k.ToLowerInvariant()).ToList(),
                ClientProtoFiles = richResolutions.Select(r => r.Entity.ToLowerInvariant()).ToList(),
            });
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

            var controllerModel = new ControllerFileModel { Namespace = ns, Name = name, Protect = spec.Auth?.Protect == true };
            var controller = TemplateEngine.Render(Templates.Controller, controllerModel);
            written.Add(WriteFile(Path.Combine(outputDir, "Controllers", name + "sController.cs"), controller));

            // gRPC server-side: bu entity'yi diğer servislerin okuyabilmesi için otomatik expose eder
            // (opt-in/opt-out yok — sıralama bağımlılığından kaçınmak için her entity her zaman expose edilir).
            var protoFields = BuildProtoFields(entity);
            var protoModel = new EntityProtoFileModel
            {
                Namespace = ns,
                Package = spec.Service.ToLowerInvariant(),
                Entity = name,
                Fields = protoFields,
            };
            written.Add(WriteFile(
                Path.Combine(outputDir, "Protos", name.ToLowerInvariant() + ".proto"),
                TemplateEngine.Render(Templates.ProtoServer, protoModel)));

            var grpcServiceModel = new GrpcServerServiceFileModel { Namespace = ns, Entity = name, Fields = protoFields };
            written.Add(WriteFile(
                Path.Combine(outputDir, "Grpc", name + "GrpcService.cs"),
                TemplateEngine.Render(Templates.GrpcServerService, grpcServiceModel)));
            grpcServerEntities.Add(name);
        }

        // Program.cs + host dosyaları
        var programModel = new ProgramFileModel
        {
            Namespace = ns,
            ContextName = contextName,
            Title = ns + " API",
            DescriptionLiteral = CSharpLiteral(BuildDescription(spec, ns)),
            HasAuth = spec.Auth is not null,
            Authority = spec.Auth?.Authority ?? string.Empty,
            Audience = spec.Auth?.Audience ?? string.Empty,
            RequireHttpsMetadata = spec.Auth?.RequireHttpsMetadata ?? false,
            GrpcServerEntities = grpcServerEntities,
            GrpcClients = richResolutions,
        };
        written.Add(WriteFile(Path.Combine(outputDir, "Program.cs"), TemplateEngine.Render(Templates.Program, programModel)));

        var host = new HostFileModel { Namespace = ns, Service = spec.Service, Database = spec.Database, GrpcClients = richResolutions };
        written.Add(WriteFile(Path.Combine(outputDir, "appsettings.json"), TemplateEngine.Render(Templates.AppSettings, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "Properties", "launchSettings.json"), TemplateEngine.Render(Templates.LaunchSettings, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "Dockerfile"), TemplateEngine.Render(Templates.Dockerfile, host)));
        written.Add(WriteFile(Path.Combine(outputDir, ".dockerignore"), TemplateEngine.Render(Templates.DockerIgnore, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "docker-compose.yml"), TemplateEngine.Render(Templates.DockerCompose, host)));
        written.Add(WriteFile(Path.Combine(outputDir, "docker-compose.snippet.yml"), TemplateEngine.Render(Templates.ComposeSnippet, host)));

        // gRPC client'ları (via: grpc olan dış referanslar için). Rich (kardeş spec veya identity/User
        // özel durumu bulunan) ise gerçek proto+client üretilir; aksi halde minimal (yalnızca Id) fallback.
        foreach (var resolution in externalRefResolutions)
        {
            if (resolution.IsRich)
            {
                var protoText = string.Equals(resolution.ProviderNamespace, "Identity", StringComparison.Ordinal)
                    ? ReadIdentityUserProtoText()
                    : TemplateEngine.Render(Templates.ProtoServer, new EntityProtoFileModel
                    {
                        Namespace = resolution.ProviderNamespace,
                        Package = resolution.ProviderNamespace.ToLowerInvariant(),
                        Entity = resolution.Entity,
                        Fields = resolution.Fields,
                    });

                written.Add(WriteFile(Path.Combine(outputDir, "Protos", resolution.Entity.ToLowerInvariant() + ".proto"), protoText));
                written.Add(WriteFile(
                    Path.Combine(outputDir, "Integration", resolution.Entity + "Client.cs"),
                    TemplateEngine.Render(Templates.GrpcClientRich, resolution)));
            }
            else
            {
                var stub = new GrpcStubFileModel { Namespace = ns, Target = resolution.Target, Entity = resolution.Entity };
                written.Add(WriteFile(
                    Path.Combine(outputDir, "Integration", resolution.Entity + "Client.cs"),
                    TemplateEngine.Render(Templates.GrpcStub, stub)));
            }
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

    private static string BuildDescription(ServiceSpec spec, string ns)
    {
        var entities = string.Join(", ", spec.Entities.Keys);
        return
            $"# {ns} API\n\n" +
            $"BaseForge ile üretilen **{spec.Service}** mikroservisi · _{spec.Service} microservice generated by BaseForge_.\n\n" +
            "## 🇹🇷 Türkçe\n" +
            $"Bu servis **{spec.Service}** bounded context'ini yönetir ve şu kaynaklar için CRUD uçları sunar: **{entities}**. " +
            "Her kaynak için oluşturma, güncelleme, silme (soft delete), tekil getirme ve listeleme uçları vardır.\n\n" +
            "Mimari: Clean Architecture + CQRS (MediatR). Veri erişimi: EF Core (+ karmaşık sorgular için Dapper). " +
            $"Veritabanı: PostgreSQL (servise özel, `{spec.Database}`). Başka servislere referanslar yalnızca ID iledir (cross-DB FK yoktur).\n\n" +
            "## 🇬🇧 English\n" +
            $"The **{spec.Service}** microservice owns the **{spec.Service}** bounded context and exposes CRUD endpoints for: **{entities}**. " +
            "Each resource provides create, update, delete (soft delete), get-by-id and list endpoints.\n\n" +
            "Architecture: Clean Architecture + CQRS (MediatR). Data access: EF Core (+ Dapper for complex queries). " +
            $"Database: PostgreSQL (per service, `{spec.Database}`). References to other services are ID-only (no cross-DB foreign keys).";
    }

    private static string CSharpLiteral(string text)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => string.Empty,
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }

        sb.Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Servisin tüm <c>via: grpc</c> dış referanslarını çözümler. Kardeş <c>spec.yaml</c> veya
    /// <c>identity/User</c> özel durumu bulunursa zengin (gerçek alanlı), bulunamazsa minimal
    /// (yalnızca Id) bir sonuç döner — hiçbir durumda hata fırlatmaz.
    /// </summary>
    private static List<GrpcClientResolution> ResolveExternalRefs(ServiceSpec spec, string ns, string? specPath)
    {
        var resolutions = new Dictionary<string, GrpcClientResolution>(StringComparer.Ordinal);

        foreach (var entity in spec.Entities.Values)
        {
            foreach (var externalRef in entity.ExternalRefs.Values)
            {
                if (!string.Equals(externalRef.Via, "grpc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = externalRef.Target;
                var entityName = NameUtil.Pascal(
                    target.Contains('/', StringComparison.Ordinal) ? target[(target.LastIndexOf('/') + 1)..] : target);

                if (resolutions.ContainsKey(entityName))
                {
                    // Bilinen kısıt: iki farklı kaynaktan aynı adlı entity'ye dış referans çakışır; ilki kazanır.
                    continue;
                }

                resolutions[entityName] = TryResolveIdentityUser(target, ns, entityName)
                    ?? TryResolveSibling(target, ns, specPath, entityName)
                    ?? new GrpcClientResolution { Namespace = ns, Entity = entityName, Target = target, IsRich = false };
            }
        }

        return [.. resolutions.Values];
    }

    /// <summary><c>identity/User</c> özel durumu — Identity'nin sabit (ApplicationUser) alan şekliyle çözümler.</summary>
    private static GrpcClientResolution? TryResolveIdentityUser(string target, string ns, string entityName)
    {
        if (!string.Equals(target, "identity/User", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fields = new List<ProtoFieldModel>
        {
            MakeProtoField("UserName", "string", 2),
            MakeProtoField("Email", "string", 3),
            MakeProtoField("FullName", "string", 4),
        };

        return new GrpcClientResolution
        {
            Namespace = ns,
            Entity = entityName,
            Target = target,
            IsRich = true,
            ProviderNamespace = "Identity",
            ConfigKey = "Identity",
            ProviderHost = "identity",
            Fields = fields,
        };
    }

    /// <summary>Kardeş <c>{servis}.yaml</c> dosyasını (spec'in bulunduğu klasörde) arayıp hedef entity'yi çözümler.</summary>
    private static GrpcClientResolution? TryResolveSibling(string target, string ns, string? specPath, string entityName)
    {
        if (string.IsNullOrWhiteSpace(specPath) || !target.Contains('/', StringComparison.Ordinal))
        {
            return null;
        }

        var serviceSegment = target[..target.IndexOf('/', StringComparison.Ordinal)];
        if (string.Equals(serviceSegment, "identity", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var specDir = Path.GetDirectoryName(Path.GetFullPath(specPath));
        if (specDir is null)
        {
            return null;
        }

        // İki spec yerleşim konvansiyonu da denenir:
        //  1) Düz kardeş dosya:  {specDir}/{segment}.yaml            (örn. samples/*.yaml — CLI)
        //  2) İç içe alt klasör: {specDir}/../{segment}/spec.yaml    (örn. Designer: her servis kendi
        //     çalışma dizini altında ayrı bir alt klasöre üretiliyor, bkz. DesignerEndpoints.ResolveOutput)
        var candidates = new[]
        {
            Path.Combine(specDir, serviceSegment + ".yaml"),
            Path.GetFullPath(Path.Combine(specDir, "..", serviceSegment, "spec.yaml")),
        };
        var siblingPath = candidates.FirstOrDefault(File.Exists);

        if (siblingPath is null)
        {
            Console.Error.WriteLine(
                $"Uyarı: '{target}' için kardeş spec bulunamadı (denenen: {string.Join(" veya ", candidates)}); minimal (yalnızca Id) client üretiliyor.");
            return null;
        }

        ServiceSpec siblingSpec;
        try
        {
            siblingSpec = SpecLoader.Load(siblingPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Uyarı: '{siblingPath}' okunamadı ({ex.Message}); minimal (yalnızca Id) client üretiliyor.");
            return null;
        }

        var targetEntity = siblingSpec.Entities
            .FirstOrDefault(e => string.Equals(e.Key, entityName, StringComparison.OrdinalIgnoreCase)).Value;

        if (targetEntity is null)
        {
            Console.Error.WriteLine($"Uyarı: '{target}' kardeş spec'te bulunamadı; minimal (yalnızca Id) client üretiliyor.");
            return null;
        }

        var providerNs = NameUtil.Pascal(siblingSpec.Service);
        return new GrpcClientResolution
        {
            Namespace = ns,
            Entity = entityName,
            Target = target,
            IsRich = true,
            ProviderNamespace = providerNs,
            ConfigKey = providerNs,
            ProviderHost = siblingSpec.Service.ToLowerInvariant(),
            Fields = BuildProtoFields(targetEntity),
        };
    }

    /// <summary>Identity'nin gömülü <c>user.proto</c> kaynağını okuyup tüketen servisin adına uyarlar.</summary>
    private static string ReadIdentityUserProtoText()
    {
        var assembly = typeof(CodeGenerator).Assembly;
        using var stream = assembly.GetManifestResourceStream("identity/Protos/user.proto")
            ?? throw new InvalidOperationException("identity/Protos/user.proto gömülü kaynağı bulunamadı.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace(IdentityReferenceNamespace, "Identity", StringComparison.Ordinal);
    }

    /// <summary>Entity'nin props'undan proto mesaj alanlarını üretir (id=1, sonrakiler 2..n).</summary>
    private static List<ProtoFieldModel> BuildProtoFields(EntitySpec entity)
    {
        var fields = new List<ProtoFieldModel>();
        var number = 2;
        foreach (var (propName, propType) in entity.Props)
        {
            fields.Add(MakeProtoField(NameUtil.Pascal(propName), propType, number++));
        }

        return fields;
    }

    private static ProtoFieldModel MakeProtoField(string name, string specType, int number)
    {
        var csharpType = TypeMap.ToCSharp(specType);
        return new ProtoFieldModel
        {
            Name = name,
            ProtoName = ToSnakeCase(name),
            ProtoType = ProtoTypeMap.ToProto(specType),
            Number = number,
            CSharpType = csharpType,
            Init = string.Equals(csharpType, "string", StringComparison.Ordinal) ? " = string.Empty;" : string.Empty,
            ToProtoExpr = ProtoTypeMap.ToProtoExpr(specType, $"value.{name}"),
            FromProtoExpr = ProtoTypeMap.FromProtoExpr(specType, $"response.{name}"),
        };
    }

    /// <summary>PascalCase'i proto konvansiyonu snake_case'e çevirir (örn. <c>UnitPrice</c> → <c>unit_price</c>).</summary>
    private static string ToSnakeCase(string pascal)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
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
