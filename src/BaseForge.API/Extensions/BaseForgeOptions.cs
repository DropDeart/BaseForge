using System.Reflection;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.Extensions;

/// <summary>
/// <see cref="ServiceCollectionExtensions.AddBaseForge"/> için yapılandırma seçenekleri.
/// Akıcı (fluent) metotlarla doldurulur:
/// <c>UsePostgreSQL</c>, <c>EnableCQRS</c>, <c>EnableAuditLog</c>, <c>EnableJwt</c>.
/// </summary>
public sealed class BaseForgeOptions
{
    internal Action<IServiceCollection>? InfrastructureRegistration { get; private set; }

    internal bool CqrsEnabled { get; private set; }

    internal Assembly[] HandlerAssemblies { get; private set; } = [];

    internal bool AuditLogEnabled { get; private set; }

    internal JwtOptions? Jwt { get; private set; }

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
}

/// <summary>
/// JWT bearer doğrulama ayarları. Merkezi Identity Service tarafından üretilen token'lar
/// her serviste lokal olarak (merkezi DB çağrısı olmadan) doğrulanır.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Beklenen token issuer (iss) değeri.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Beklenen token audience (aud) değeri.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Token imzasını doğrulamak için kullanılan simetrik anahtar.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
