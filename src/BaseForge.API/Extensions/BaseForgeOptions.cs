using System.Reflection;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Extensions;
using BaseForge.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.Extensions;

/// <summary>
/// <see cref="ServiceCollectionExtensions.AddBaseForge"/> için yapılandırma seçenekleri.
/// Akıcı (fluent) metotlarla doldurulur:
/// <c>UsePostgreSQL</c>, <c>EnableCQRS</c>, <c>EnableAuditLog</c>, <c>EnableJwt</c>, <c>EnableRabbitMq</c>.
/// </summary>
public sealed class BaseForgeOptions
{
    internal Action<IServiceCollection>? InfrastructureRegistration { get; private set; }

    internal string? ConnectionString { get; private set; }

    internal bool CqrsEnabled { get; private set; }

    internal Assembly[] HandlerAssemblies { get; private set; } = [];

    internal bool AuditLogEnabled { get; private set; }

    internal bool MultiTenancyEnabled { get; private set; }

    internal JwtOptions? Jwt { get; private set; }

    internal RabbitMqOptions? RabbitMq { get; private set; }

    /// <summary>
    /// PostgreSQL veri erişimini etkinleştirir ve uygulamanın <typeparamref name="TContext"/>'ini,
    /// repository'leri, <c>UnitOfWork</c> ve Dapper sorgu yardımcısını kaydeder.
    /// </summary>
    /// <typeparam name="TContext">Uygulamanın <see cref="BaseForgeDbContext"/>'ten türeyen context tipi.</typeparam>
    /// <param name="connectionString">PostgreSQL bağlantı dizesi.</param>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions UsePostgreSQL<TContext>(string connectionString)
        where TContext : BaseForgeDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        InfrastructureRegistration = services => services.AddBaseForgeInfrastructure<TContext>(connectionString);
        return this;
    }

    /// <summary>
    /// MediatR tabanlı CQRS'i etkinleştirir. Handler'lar verilen assembly'lerden taranır;
    /// hiçbiri verilmezse çalışan uygulamanın (entry) assembly'si kullanılır.
    /// </summary>
    /// <param name="handlerAssemblies">Command/query handler'larının bulunduğu assembly'ler.</param>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions EnableCQRS(params Assembly[] handlerAssemblies)
    {
        CqrsEnabled = true;
        HandlerAssemblies = handlerAssemblies is { Length: > 0 }
            ? handlerAssemblies
            : [Assembly.GetEntryAssembly()
                ?? throw new InvalidOperationException(
                    "Handler assembly tespit edilemedi. EnableCQRS çağrısına en az bir assembly geçin.")];
        return this;
    }

    /// <summary>
    /// Audit (<c>CreatedBy</c>) bilgisinin o anki kullanıcıdan doldurulmasını etkinleştirir;
    /// <see cref="Core.Interfaces.ICurrentUser"/>'ı HttpContext tabanlı implementasyonla kaydeder.
    /// </summary>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions EnableAuditLog()
    {
        AuditLogEnabled = true;
        return this;
    }

    /// <summary>
    /// Multi-tenancy'yi etkinleştirir: <see cref="Core.Interfaces.ICurrentTenant"/>'ı HttpContext/JWT
    /// (<c>tenant_id</c> claim'i) tabanlı implementasyonla kaydeder. <see cref="Infrastructure.Data.BaseForgeDbContext"/>
    /// bunu <c>ITenantEntity</c> uygulayan entity'lerde otomatik <c>TenantId</c> damgalama ve
    /// query filter için kullanır.
    /// </summary>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions EnableMultiTenancy()
    {
        MultiTenancyEnabled = true;
        return this;
    }

    /// <summary>JWT bearer kimlik doğrulamasını yapılandırıp etkinleştirir.</summary>
    /// <param name="configure">JWT ayarlarını dolduran delege (issuer, audience, signing key).</param>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions EnableJwt(Action<JwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var jwt = new JwtOptions();
        configure(jwt);
        Jwt = jwt;
        return this;
    }

    /// <summary>
    /// RabbitMQ tabanlı asenkron olay yayınlama/tüketmeyi (<see cref="Core.Messaging.IEventBus"/>)
    /// etkinleştirir. Abonelik varsa (<see cref="RabbitMqOptions.Subscribe{TEvent}"/>) bir tüketici
    /// hosted service'i de otomatik eklenir.
    /// </summary>
    /// <param name="configure">Broker bağlantı ve abonelik ayarlarını dolduran delege.</param>
    /// <returns>Zincirleme için aynı seçenek nesnesi.</returns>
    public BaseForgeOptions EnableRabbitMq(Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var rabbitMq = new RabbitMqOptions();
        configure(rabbitMq);
        RabbitMq = rabbitMq;
        return this;
    }
}

/// <summary>
/// JWT bearer doğrulama ayarları. Merkezi Identity Service tarafından üretilen token'lar
/// her serviste lokal olarak (merkezi DB çağrısı olmadan) doğrulanır.
/// İki mod: <see cref="Authority"/> verilirse asimetrik/JWKS (önerilen, merkez auth ile),
/// aksi halde <see cref="SigningKey"/> ile simetrik (HMAC).
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Merkezi Identity Service'in adresi (örn. <c>http://identity:8080</c>). Verilirse imza
    /// doğrulaması, bu adresin OpenID discovery + JWKS uçlarından otomatik (asimetrik) yapılır.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>Discovery/JWKS için HTTPS zorunlu mu? Container içi HTTP'de <see langword="false"/> yapın.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Beklenen token audience (aud) değeri.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Beklenen token issuer (iss) değeri. Yalnızca simetrik modda gereklidir.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Simetrik (HMAC) imza anahtarı. Yalnızca <see cref="Authority"/> verilmediğinde kullanılır.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
