namespace BaseForge.CodeGen.Spec;

/// <summary>Bir mikroservisin (bounded context) tam tanımı. Tek YAML dosyası = tek servis = tek DB.</summary>
public sealed class ServiceSpec
{
    /// <summary>Servis adı (örn. <c>orders</c>). Proje/namespace bundan türetilir.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Servisin kendi veritabanı adı (Database per Service).</summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>Servise ait entity'ler (anahtar = entity adı).</summary>
    public Dictionary<string, EntitySpec> Entities { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Merkez auth (JWT) entegrasyonu. Verilirse üretilen servis EnableJwt + [Authorize] ile gelir.</summary>
    public ServiceAuthSpec? Auth { get; set; }

    /// <summary>Docker host portları (opsiyonel). Boş alanlar için varsayılanlar kullanılır.</summary>
    public DockerPortsSpec? DockerPorts { get; set; }
}

/// <summary>
/// Üretilen <c>docker-compose.yml</c>'da host tarafına açılan portlar. Tüm alanlar opsiyonel —
/// boş bırakılırsa çağıran taraf (CodeGenerator/IdentityGenerator) kendi varsayılanını kullanır.
/// Aynı makinede başka projelerle port çakışmasını önlemek için verilir.
/// </summary>
public sealed class DockerPortsSpec
{
    /// <summary>REST API'nin host portu (container-içi bind portu sabit kalır, sadece host mapping'i değişir).</summary>
    public int? Rest { get; set; }

    /// <summary>gRPC'nin host portu.</summary>
    public int? Grpc { get; set; }

    /// <summary>PostgreSQL'in host portu.</summary>
    public int? Postgres { get; set; }
}

/// <summary>Üretilen servisin merkez Identity'ye JWT ile bağlanma ayarları.</summary>
public sealed class ServiceAuthSpec
{
    /// <summary>Merkez Identity adresi (discovery/JWKS); örn. <c>http://localhost:5090</c>.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Beklenen audience; örn. <c>baseforge-api</c>.</summary>
    public string Audience { get; set; } = "baseforge-api";

    /// <summary>HTTPS metadata zorunlu mu? Container içi HTTP'de <see langword="false"/>.</summary>
    public bool RequireHttpsMetadata { get; set; }

    /// <summary>Controller'lar [Authorize] ile korunsun mu? (varsayılan: evet).</summary>
    public bool Protect { get; set; } = true;
}

/// <summary>Servise ait bir entity tanımı.</summary>
public sealed class EntitySpec
{
    /// <summary>
    /// Alanlar: ad -> tip tanımı. YAML'da düz string (<c>total: decimal</c>) veya zengin obje
    /// (<c>total: { type: decimal, nullable: true }</c>) olarak yazılabilir (bkz. <see cref="PropSpecYamlConverter"/>).
    /// Id/audit alanları BaseEntity'den gelir.
    /// </summary>
    public Dictionary<string, PropSpec> Props { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Aynı servis içindeki diğer entity'lerle ilişkiler (gerçek FK üretilir).</summary>
    public Dictionary<string, RelationSpec> Relations { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Başka servislere yapılan, yalnızca ID ile referanslar (FK/navigation üretilmez).</summary>
    public Dictionary<string, ExternalRefSpec> ExternalRefs { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Servis içi iki entity arasındaki ilişki.</summary>
public sealed class RelationSpec
{
    /// <summary>İlişki türü: <c>one-to-many</c>, <c>many-to-one</c> veya <c>one-to-one</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>İlişkinin hedef entity'si (aynı servis içinde tanımlı olmalı).</summary>
    public string Target { get; set; } = string.Empty;
}

/// <summary>
/// Bir entity alanının tam tanımı: tip + (opsiyonel) nullable/maxLength/default.
/// YAML'da düz string (<c>string</c>) olarak da, zengin obje olarak da yazılabilir —
/// <see cref="PropSpecYamlConverter"/> ikisini de okur/yazar.
/// </summary>
public sealed class PropSpec
{
    /// <summary>Spec tipi (örn. <c>string</c>, <c>decimal</c>, <c>guid</c>).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Alan NULL olabilir mi? Varsayılan <see langword="false"/> (NOT NULL).</summary>
    public bool Nullable { get; set; }

    /// <summary>
    /// Yalnızca <c>string</c>/<c>text</c> tipinde anlamlıdır — EF Core <c>[MaxLength]</c> üretir
    /// (Postgres <c>character varying(n)</c>, aksi halde sınırsız <c>text</c>).
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// C# tarafında varsayılan değer (yalnızca in-memory initializer; DB-level default değildir).
    /// string/int/long/short/decimal/double/float/bool için desteklenir; datetime/date/guid'de geçersizdir.
    /// </summary>
    public string? Default { get; set; }
}

/// <summary>Başka bir servise ait kayda yapılan dış referans. FK/navigation üretilmez; yalnızca ID tutulur.</summary>
public sealed class ExternalRefSpec
{
    /// <summary>Hedef: <c>servis/Entity</c> (örn. <c>customers/Customer</c>). Belgeleme amaçlıdır.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Bu serviste tutulacak ID alanının adı (örn. <c>CustomerId</c>).</summary>
    public string Store { get; set; } = string.Empty;

    /// <summary>Veriye erişim biçimi: <c>grpc</c> (senkron) veya <c>event</c> (asenkron).</summary>
    public string Via { get; set; } = "grpc";
}
