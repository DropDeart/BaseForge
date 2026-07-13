using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Generation;

/// <summary>Bir <see cref="ServiceSpec"/>'ten mikroservis kod iskelesini üretir.</summary>
internal static class CodeGenerator
{
    /// <summary>Üretilen kodun referans verdiği BaseForge NuGet paket sürümü.</summary>
    private const string BaseForgeVersion = "0.2.1-alpha";

    /// <summary>Identity'nin gömülü <c>user.proto</c> kaynağındaki referans namespace'i.</summary>
    private const string IdentityReferenceNamespace = "BaseForge.Identity";

    /// <summary>
    /// Rich gRPC client'ların ve RabbitMq'nun varsayılan cross-service host adresi. Her üretilen servis
    /// kendi izole docker-compose ağında çalıştığından (bkz. <c>Templates.DockerCompose</c>), sağlayıcı
    /// servisin bare adı (örn. <c>"identity"</c>) network DNS ile çözülemez — Docker Desktop'ın host'a
    /// yönlendiren <c>host.docker.internal</c> adresi kullanılır (JWT <c>Authority</c> ile aynı düzeltme).
    /// </summary>
    private const string CrossServiceHost = "host.docker.internal";

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

        // Olay abonelikleri (subscribes) önceden çözümle — Program.cs render'ı buna bağlı.
        var subscriptionResolutions = ResolveSubscriptions(spec, ns, specPath);
        var hasRabbitMq = spec.Entities.Values.Any(e => e.Publishes.Count > 0) || subscriptionResolutions.Count > 0;

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
            var code = TemplateEngine.Render(Templates.Entity, BuildEntityModel(ns, name, entity, spec.MultiTenant));
            written.Add(WriteFile(Path.Combine(outputDir, "Entities", name + ".cs"), code));

            var counters = BuildCounterNames(entity);
            var fields = BuildScalars(entity);
            var feature = new FeatureFileModel
            {
                Namespace = ns,
                Name = name,
                Fields = fields,
                Service = spec.Service.ToLowerInvariant(),
                PublishCreated = entity.Publishes.Any(p => string.Equals(p, "created", StringComparison.OrdinalIgnoreCase)),
                PublishUpdated = entity.Publishes.Any(p => string.Equals(p, "updated", StringComparison.OrdinalIgnoreCase)),
                PublishDeleted = entity.Publishes.Any(p => string.Equals(p, "deleted", StringComparison.OrdinalIgnoreCase)),
                IncludeUpdate = !entity.AppendOnly,
                IncludeDelete = !entity.AppendOnly,
                Counters = counters,
                SearchPredicate = entity.Searchable ? BuildSearchPredicate(fields) : null,
                Paginated = entity.Paginated,
                Sortable = entity.Sortable,
            };
            var featureDir = Path.Combine(outputDir, "Features", name + "s");
            written.Add(WriteFile(Path.Combine(featureDir, name + "Dto.cs"), TemplateEngine.Render(Templates.Dto, feature)));
            written.Add(WriteFile(Path.Combine(featureDir, name + "Commands.cs"), TemplateEngine.Render(Templates.Commands, feature)));
            written.Add(WriteFile(Path.Combine(featureDir, name + "Queries.cs"), TemplateEngine.Render(Templates.Queries, feature)));
            if (feature.HasAnyPublish)
            {
                written.Add(WriteFile(Path.Combine(featureDir, name + "Events.cs"), TemplateEngine.Render(Templates.Events, feature)));
            }

            var isProtected = spec.Auth?.Protect == true;
            var controllerModel = new ControllerFileModel
            {
                Namespace = ns,
                Name = name,
                Protect = isProtected,
                AnonymousList = isProtected && HasAnonymousAction(entity, "list"),
                AnonymousGetById = isProtected && HasAnonymousAction(entity, "getById"),
                AnonymousCreate = isProtected && HasAnonymousAction(entity, "create"),
                AnonymousUpdate = isProtected && HasAnonymousAction(entity, "update"),
                AnonymousDelete = isProtected && HasAnonymousAction(entity, "delete"),
                IncludeUpdate = !entity.AppendOnly,
                IncludeDelete = !entity.AppendOnly,
                Counters = counters,
                Paginated = entity.Paginated,
            };
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

        // Genel görsel yükleme ucu (entity'den bağımsız) — her servis için bir kere üretilir.
        var mediaController = TemplateEngine.Render(
            Templates.MediaController,
            new ControllerFileModel { Namespace = ns, Protect = spec.Auth?.Protect == true });
        written.Add(WriteFile(Path.Combine(outputDir, "Controllers", "MediaController.cs"), mediaController));

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
            HasRabbitMq = hasRabbitMq,
            Subscriptions = subscriptionResolutions,
            HasMultiTenancy = spec.MultiTenant,
        };
        written.Add(WriteFile(Path.Combine(outputDir, "Program.cs"), TemplateEngine.Render(Templates.Program, programModel)));

        var host = new HostFileModel
        {
            Namespace = ns,
            Service = spec.Service,
            ServiceKey = spec.Service.ToLowerInvariant(),
            Database = spec.Database,
            GrpcClients = richResolutions,
            RestPort = spec.DockerPorts?.Rest ?? 8080,
            GrpcPort = spec.DockerPorts?.Grpc ?? 8081,
            PostgresPort = spec.DockerPorts?.Postgres ?? 5432,
            HasRabbitMq = hasRabbitMq,
            CorsOrigins = spec.CorsOrigins,
        };
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

        // Olay abonelikleri (subscribes) — her biri için gölge event/data class'ı + INotificationHandler stub'ı.
        foreach (var subscription in subscriptionResolutions)
        {
            written.Add(WriteFile(
                Path.Combine(outputDir, "Integration", subscription.Handler + ".cs"),
                TemplateEngine.Render(Templates.SubscriptionHandler, subscription)));
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

        // Workspace kökündeki paylaşılan kayda ekle — identity dashboard'unun "Servisler" bölümü bunu okur.
        ServiceRegistry.UpsertService(outputDir, spec);

        return written;
    }

    /// <summary>Entity'nin 'anonymousActions' listesinde verilen action adı geçiyor mu (büyük/küçük harf duyarsız)?</summary>
    private static bool HasAnonymousAction(EntitySpec entity, string action)
        => entity.AnonymousActions.Contains(action, StringComparer.OrdinalIgnoreCase);

    /// <summary>Entity'nin 'counters' listesindeki alan adlarını, prop'taki gerçek yazımıyla (PascalCase) çözümler.</summary>
    private static List<string> BuildCounterNames(EntitySpec entity)
        => entity.Counters
            .Select(counter => entity.Props.Keys.First(p => string.Equals(p, counter, StringComparison.OrdinalIgnoreCase)))
            .Select(NameUtil.Pascal)
            .ToList();

    private static EntityFileModel BuildEntityModel(string ns, string name, EntitySpec entity, bool multiTenant)
    {
        var model = new EntityFileModel
        {
            Namespace = ns,
            Name = name,
            Scalars = BuildScalars(entity),
            IsMultiTenant = multiTenant,
        };

        if (multiTenant)
        {
            // TenantId kullanıcı tarafından YAML'da tanımlanmaz — BaseForgeDbContext tarafından
            // Added durumundaki entity'lere otomatik damgalanır (bkz. ApplyAuditAndSoftDelete).
            // Bu yüzden yalnızca entity sınıfına eklenir, Create/Update komut DTO'larına (Fields) değil.
            model.Scalars.Add(new ScalarModel { Name = "TenantId", Type = "Guid" });
        }

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
            ProviderHost = CrossServiceHost,
            Fields = fields,
        };
    }

    /// <summary>Kardeş <c>{servis}.yaml</c> dosyasını (spec'in bulunduğu klasörde) arayıp hedef entity'yi çözümler.</summary>
    private static GrpcClientResolution? TryResolveSibling(string target, string ns, string? specPath, string entityName)
    {
        if (!target.Contains('/', StringComparison.Ordinal))
        {
            return null;
        }

        var serviceSegment = target[..target.IndexOf('/', StringComparison.Ordinal)];
        if (string.Equals(serviceSegment, "identity", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var siblingSpec = LoadSiblingSpec(serviceSegment, specPath, target);
        if (siblingSpec is null)
        {
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
            ProviderHost = CrossServiceHost,
            Fields = BuildProtoFields(targetEntity),
        };
    }

    /// <summary>
    /// Kardeş <c>{servis}.yaml</c>/<c>spec.yaml</c> dosyasını (spec'in bulunduğu klasöre göre, iki yerleşim
    /// konvansiyonuyla) bulup yükler. gRPC dış referans çözümlemesi (<see cref="TryResolveSibling"/>) ve
    /// olay aboneliği çözümlemesi (<see cref="ResolveSubscriptions"/>) tarafından ortak kullanılır.
    /// Bulunamaz/okunamazsa uyarı yazıp <see langword="null"/> döner — hiçbir çağıran taraf exception görmez.
    /// </summary>
    private static ServiceSpec? LoadSiblingSpec(string serviceSegment, string? specPath, string referenceForWarning)
    {
        if (string.IsNullOrWhiteSpace(specPath))
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
                $"Uyarı: '{referenceForWarning}' için kardeş spec bulunamadı (denenen: {string.Join(" veya ", candidates)}); minimal çözümleme kullanılıyor.");
            return null;
        }

        try
        {
            return SpecLoader.Load(siblingPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Uyarı: '{siblingPath}' okunamadı ({ex.Message}); minimal çözümleme kullanılıyor.");
            return null;
        }
    }

    /// <summary>
    /// <c>spec.Subscribes</c>'taki her aboneliği çözümler. Hedef (kardeş veya kendi) spec'te entity
    /// bulunur ve o entity'nin <c>publishes</c> listesinde ilgili Kind varsa zengin (gerçek alanlı),
    /// aksi halde minimal (yalnızca Id) bir sonuç döner — hiçbir durumda hata fırlatmaz.
    /// </summary>
    private static List<SubscriptionResolution> ResolveSubscriptions(ServiceSpec spec, string ns, string? specPath)
    {
        var resolutions = new List<SubscriptionResolution>();
        if (spec.Subscribes is null)
        {
            return resolutions;
        }

        foreach (var subscribe in spec.Subscribes)
        {
            var (serviceSegment, sourceEntityName, kind) = ParseEventReference(subscribe.Event);
            if (serviceSegment is null)
            {
                Console.Error.WriteLine($"Uyarı: '{subscribe.Event}' geçersiz olay referansı (beklenen: 'servis/EntityCreated|Updated|Deleted'); atlanıyor.");
                continue;
            }

            // Kendi kendine abonelik: kardeş spec dosyası aramak yerine doğrudan bu servisin spec'i kullanılır.
            var sourceSpec = string.Equals(serviceSegment, spec.Service, StringComparison.OrdinalIgnoreCase)
                ? spec
                : LoadSiblingSpec(serviceSegment, specPath, subscribe.Event);

            var sourceEntity = sourceSpec?.Entities
                .FirstOrDefault(e => string.Equals(e.Key, sourceEntityName, StringComparison.OrdinalIgnoreCase)).Value;

            var isRich = sourceEntity is not null
                && sourceEntity.Publishes.Any(p => string.Equals(p, kind, StringComparison.OrdinalIgnoreCase));

            if (sourceEntity is not null && !isRich)
            {
                Console.Error.WriteLine(
                    $"Uyarı: '{subscribe.Event}' — '{sourceEntityName}' entity'si bu olayı ('{kind}') 'publishes' listesinde tanımlamıyor; minimal (yalnızca Id) çözümleme kullanılıyor.");
            }

            resolutions.Add(new SubscriptionResolution
            {
                Namespace = ns,
                Handler = NameUtil.Pascal(subscribe.Handler),
                SourceService = serviceSegment,
                SourceEntity = NameUtil.Pascal(sourceEntityName),
                Kind = kind,
                EventType = subscribe.Event,
                IsRich = isRich,
                Fields = isRich ? BuildScalars(sourceEntity!) : [],
            });
        }

        return resolutions;
    }

    /// <summary>
    /// <c>servis/EntityKind</c> biçimindeki bir olay referansını ayrıştırır
    /// (örn. <c>blog/CommentCreated</c> → <c>("blog", "Comment", "Created")</c>).
    /// Ayrıştırılamazsa (<c>/</c> yok veya bilinen bir Kind son eki bulunamıyorsa) <c>ServiceSegment</c> null döner.
    /// </summary>
    private static (string? ServiceSegment, string EntityName, string Kind) ParseEventReference(string eventRef)
    {
        var slashIndex = eventRef.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex < 0)
        {
            return (null, string.Empty, string.Empty);
        }

        var serviceSegment = eventRef[..slashIndex];
        var rest = eventRef[(slashIndex + 1)..];

        foreach (var kind in new[] { "Created", "Updated", "Deleted" })
        {
            if (rest.EndsWith(kind, StringComparison.Ordinal) && rest.Length > kind.Length)
            {
                return (serviceSegment, rest[..^kind.Length], kind);
            }
        }

        return (null, string.Empty, string.Empty);
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
        foreach (var (propName, prop) in entity.Props)
        {
            fields.Add(MakeProtoField(NameUtil.Pascal(propName), prop.Type, number++, prop.Nullable));
        }

        return fields;
    }

    private static ProtoFieldModel MakeProtoField(string name, string specType, int number, bool nullable = false)
    {
        var csharpBase = TypeMap.ToCSharp(specType);
        var isNonNullableString = !nullable && string.Equals(csharpBase, "string", StringComparison.Ordinal);
        return new ProtoFieldModel
        {
            Name = name,
            ProtoName = ToSnakeCase(name),
            ProtoType = ProtoTypeMap.ToProto(specType),
            Number = number,
            CSharpType = csharpBase + (nullable ? "?" : string.Empty),
            Init = isNonNullableString ? " = string.Empty;" : string.Empty,
            ToProtoExpr = ProtoTypeMap.ToProtoExpr(specType, $"value.{name}", nullable),
            FromProtoExpr = ProtoTypeMap.FromProtoExpr(specType, $"response.{name}", nullable),
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

        foreach (var (propName, prop) in entity.Props)
        {
            var csharpBase = TypeMap.ToCSharp(prop.Type);
            scalars.Add(new ScalarModel
            {
                Name = NameUtil.Pascal(propName),
                Type = csharpBase + (prop.Nullable ? "?" : string.Empty),
                Init = ComputeInit(prop, csharpBase),
                MaxLength = prop.MaxLength,
                IsJson = prop.Type.Equals("json", StringComparison.OrdinalIgnoreCase),
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

    /// <summary>
    /// Entity'nin string/text alanları üzerinden, <c>List</c> sorgusunun <c>Search</c> filtresi için
    /// <c>||</c> ile birleştirilmiş <c>EF.Functions.ILike(...)</c> ifadesini üretir. Hiç string alan
    /// yoksa <see langword="null"/> döner (üretilen handler arama desteği sunmaz).
    /// </summary>
    private static string? BuildSearchPredicate(List<ScalarModel> fields)
    {
        var stringFields = fields.Where(f => f.Type is "string" or "string?").ToList();
        return stringFields.Count == 0
            ? null
            : string.Join(" || ", stringFields.Select(f =>
            {
                var accessor = f.Type == "string?" ? $"(x.{f.Name} ?? string.Empty)" : $"x.{f.Name}";
                return "EF.Functions.ILike(" + accessor + ", $\"%{request.Search}%\")";
            }));
    }

    /// <summary>
    /// Bir alanın C# initializer'ını hesaplar: explicit 'default' varsa tip-uygun literal;
    /// yoksa non-nullable string için mevcut <c>= string.Empty;</c> fallback'i; aksi halde boş.
    /// </summary>
    private static string ComputeInit(PropSpec prop, string csharpBaseType)
    {
        if (prop.Default is not null)
        {
            return $" = {FormatDefaultLiteral(prop.Default, csharpBaseType)};";
        }

        if (!prop.Nullable && string.Equals(csharpBaseType, "string", StringComparison.Ordinal))
        {
            return " = string.Empty;";
        }

        return string.Empty;
    }

    /// <summary>Ham (YAML string) default değerini tip-uygun bir C# literal ifadesine çevirir.</summary>
    private static string FormatDefaultLiteral(string rawDefault, string csharpBaseType) => csharpBaseType switch
    {
        "string" => $"\"{rawDefault.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        "decimal" => $"{rawDefault}m",
        "double" => $"{rawDefault}d",
        "float" => $"{rawDefault}f",
        "bool" => rawDefault.ToLowerInvariant(),
        _ => rawDefault, // int/long/short — doğrudan sayısal literal
    };

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
