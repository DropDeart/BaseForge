namespace BaseForge.CodeGen.Generation;

/// <summary>Bir entity'nin tek bir skaler kolonu (prop / FK id / dış referans id).</summary>
internal sealed class ScalarModel
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Nullable ise sonunda <c>?</c> içerir (örn. <c>decimal?</c>).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Alan başlatıcısı (örn. string için <c> = string.Empty;</c>), yoksa boş.</summary>
    public string Init { get; set; } = string.Empty;

    /// <summary>Yalnızca string/text tipinde dolu — <c>[MaxLength(n)]</c> attribute'u için.</summary>
    public int? MaxLength { get; set; }
}

/// <summary>Servis içi bir ilişki için navigation property.</summary>
internal sealed class NavModel
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsCollection { get; set; }
}

/// <summary>Entity dosyası şablonu için model.</summary>
internal sealed class EntityFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ScalarModel> Scalars { get; set; } = [];

    public List<NavModel> Navigations { get; set; } = [];
}

/// <summary>DbContext içinde bir entity referansı (DbSet üretimi için).</summary>
internal sealed class EntityRef
{
    public string Name { get; set; } = string.Empty;

    public string Plural { get; set; } = string.Empty;
}

/// <summary>DbContext dosyası şablonu için model.</summary>
internal sealed class ContextFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ContextName { get; set; } = string.Empty;

    public List<EntityRef> Entities { get; set; } = [];
}

/// <summary>.csproj şablonu için model.</summary>
internal sealed class ProjectFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string BaseForgeVersion { get; set; } = string.Empty;

    /// <summary>Kendi entity'leri için üretilen proto dosya adları (küçük harf, uzantısız) — <c>GrpcServices="Server"</c>.</summary>
    public List<string> ServerProtoFiles { get; set; } = [];

    /// <summary>Rich dış referanslar için kopyalanan proto dosya adları (küçük harf, uzantısız) — <c>GrpcServices="Client"</c>.</summary>
    public List<string> ClientProtoFiles { get; set; } = [];
}

/// <summary>CQRS feature dosyaları (Dto/Commands/Queries) için model.</summary>
internal sealed class FeatureFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Yazılabilir alanlar (props + FK id + dış ref id). Id ve audit hariç.</summary>
    public List<ScalarModel> Fields { get; set; } = [];
}

/// <summary>Controller şablonu için model.</summary>
internal sealed class ControllerFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Controller [Authorize] ile korunsun mu?</summary>
    public bool Protect { get; set; }
}

/// <summary>Program.cs şablonu için model.</summary>
internal sealed class ProgramFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string ContextName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>OpenAPI açıklaması — hazır C# string literal'i (tırnaklar dahil, escape'lenmiş).</summary>
    public string DescriptionLiteral { get; set; } = "\"\"";

    public bool HasAuth { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; }

    /// <summary>Bu servisin kendi entity'leri — her biri için <c>MapGrpcService&lt;X&gt;()</c> üretilir.</summary>
    public List<string> GrpcServerEntities { get; set; } = [];

    /// <summary>Rich çözümlenen dış referanslar — her biri için <c>AddGrpcClient&lt;...&gt;()</c> üretilir.</summary>
    public List<GrpcClientResolution> GrpcClients { get; set; } = [];
}

/// <summary>appsettings / Dockerfile / docker-compose şablonları için model.</summary>
internal sealed class HostFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Service { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="Service"/>'in küçük harfli hâli — docker-compose servis anahtarı (ve buna bağlı örtük
    /// imaj adı) olarak kullanılır. Docker registry kuralı gereği imaj adları büyük harf içeremez.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    /// <summary>Rich çözümlenen dış referanslar — appsettings <c>Grpc:{Provider}</c> adresleri için.</summary>
    public List<GrpcClientResolution> GrpcClients { get; set; } = [];

    /// <summary>REST host portu (docker-compose ports mapping'i; container-içi bind portu sabit 8080).</summary>
    public int RestPort { get; set; } = 8080;

    /// <summary>gRPC host portu (container-içi bind portu sabit 8081).</summary>
    public int GrpcPort { get; set; } = 8081;

    /// <summary>PostgreSQL host portu.</summary>
    public int PostgresPort { get; set; } = 5432;
}

/// <summary>gRPC client stub şablonu için model (fallback — kardeş spec bulunamayan durum).</summary>
internal sealed class GrpcStubFileModel
{
    public string Namespace { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;
}

/// <summary>Bir proto mesaj alanı: C# tarafı + proto tarafı + aradaki dönüşüm ifadeleri.</summary>
internal sealed class ProtoFieldModel
{
    /// <summary>C# prop adı (PascalCase) — <see cref="ScalarModel.Name"/> ile aynı.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Proto alan adı (snake_case).</summary>
    public string ProtoName { get; set; } = string.Empty;

    /// <summary>Proto3 skaler tipi (örn. <c>string</c>, <c>int32</c>).</summary>
    public string ProtoType { get; set; } = string.Empty;

    /// <summary>Proto alan numarası (id=1, sonrakiler 2..n).</summary>
    public int Number { get; set; }

    public string CSharpType { get; set; } = string.Empty;

    /// <summary>Alan başlatıcısı (örn. <c>string</c> için <c> = string.Empty;</c>), yoksa boş.</summary>
    public string Init { get; set; } = string.Empty;

    /// <summary>Proto mesajından C# değerine dönüşüm ifadesi (örn. <c>Guid.Parse(response.Id)</c>).</summary>
    public string FromProtoExpr { get; set; } = string.Empty;

    /// <summary>C# değerinden proto mesaj alanına atama ifadesi (örn. <c>value.Price.ToString(...)</c>).</summary>
    public string ToProtoExpr { get; set; } = string.Empty;
}

/// <summary>Server-side <c>.proto</c> dosyası şablonu için model (hem iş servisleri hem Identity/User için ortak).</summary>
internal sealed class EntityProtoFileModel
{
    /// <summary><c>csharp_namespace</c> — <c>{{Namespace}}.Grpc</c>.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Proto <c>package</c> adı (lowercase servis adı).</summary>
    public string Package { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public List<ProtoFieldModel> Fields { get; set; } = [];
}

/// <summary>Server-side gRPC servis implementasyonu şablonu için model.</summary>
internal sealed class GrpcServerServiceFileModel
{
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Entity adı (proto/servis sınıfı için).</summary>
    public string Entity { get; set; } = string.Empty;

    public List<ProtoFieldModel> Fields { get; set; } = [];
}

/// <summary>
/// Bir dış referansın (<see cref="ExternalRefSpec"/>) çözümlenmiş hâli — eski <see cref="GrpcStubFileModel"/>'in
/// yerini alır. Kardeş spec veya <c>identity/User</c> özel durumu bulunduysa <see cref="IsRich"/> true olur.
/// </summary>
internal sealed class GrpcClientResolution
{
    /// <summary>Tüketen (client'ı üretilen) servisin namespace'i.</summary>
    public string Namespace { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    /// <summary>Ham hedef string'i (örn. <c>"products/Product"</c>).</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Kardeş spec veya identity/User özel durumu bulunduysa true; aksi halde minimal fallback.</summary>
    public bool IsRich { get; set; }

    /// <summary>Sağlayıcı servisin namespace'i (örn. <c>"Products"</c>, <c>"Identity"</c>) — sadece rich ise dolu.</summary>
    public string ProviderNamespace { get; set; } = string.Empty;

    /// <summary>appsettings <c>Grpc:{ProviderNamespace}</c> anahtarı.</summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>Docker/host adı olarak kullanılabilir küçük harfli sağlayıcı adı (örn. <c>"products"</c>).</summary>
    public string ProviderHost { get; set; } = string.Empty;

    /// <summary>Rich ise hedef entity'nin gerçek alanları; değilse boş (yalnızca Id).</summary>
    public List<ProtoFieldModel> Fields { get; set; } = [];
}
