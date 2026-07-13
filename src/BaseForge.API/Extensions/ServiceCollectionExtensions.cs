using System.Text;
using BaseForge.API.Authentication;
using BaseForge.Core.Interfaces;
using BaseForge.Core.Messaging;
using BaseForge.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BaseForge.API.Extensions;

/// <summary>
/// BaseForge'u tek bir çağrıyla DI'a ekleyen extension metotları (Opinionated Library).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// BaseForge'u yapılandırıp kaydeder. Örnek:
    /// <code>
    /// builder.Services.AddBaseForge(options =>
    /// {
    ///     options.UsePostgreSQL&lt;AppDbContext&gt;(connectionString);
    ///     options.EnableCQRS();
    ///     options.EnableAuditLog();
    /// });
    /// </code>
    /// </summary>
    /// <param name="services">DI servis koleksiyonu.</param>
    /// <param name="configure">BaseForge seçeneklerini dolduran delege.</param>
    /// <returns>Zincirleme için aynı <paramref name="services"/>.</returns>
    public static IServiceCollection AddBaseForge(this IServiceCollection services, Action<BaseForgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BaseForgeOptions();
        configure(options);

        // 1) Veri erişimi (DbContext + repository + UnitOfWork + Dapper)
        options.InfrastructureRegistration?.Invoke(services);

        // 2) CQRS (MediatR)
        if (options.CqrsEnabled)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(options.HandlerAssemblies));
        }

        // 3) Audit için o anki kullanıcı
        if (options.AuditLogEnabled)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
        }

        // 3b) Multi-tenancy için o anki kiracı
        if (options.MultiTenancyEnabled)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentTenant, CurrentTenant>();
        }

        // 4) JWT kimlik doğrulama
        if (options.Jwt is not null)
        {
            AddJwtAuthentication(services, options.Jwt);
        }

        // 5) RabbitMQ olay yayınlama/tüketme
        if (options.RabbitMq is not null)
        {
            AddRabbitMq(services, options.RabbitMq);
        }

        // UseBaseForge'ın auth middleware'i ekleyip eklemeyeceğini bilmesi için işaret.
        services.AddSingleton(new BaseForgeFeatures { JwtEnabled = options.Jwt is not null });

        return services;
    }

    private static void AddJwtAuthentication(IServiceCollection services, JwtOptions jwt)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(jwt.Authority))
                {
                    // Asimetrik/JWKS: imza + issuer, Authority'nin discovery/JWKS'inden otomatik.
                    options.Authority = jwt.Authority;
                    options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuer = true,
                        ValidateLifetime = true,
                    };
                }
                else
                {
                    // Simetrik (HMAC).
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        ValidateLifetime = true,
                    };
                }
            });

        services.AddAuthorization();
    }

    private static void AddRabbitMq(IServiceCollection services, RabbitMqOptions rabbitMq)
    {
        services.AddSingleton(rabbitMq);
        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        if (rabbitMq.Subscriptions.Count > 0)
        {
            services.AddHostedService<RabbitMqConsumerHostedService>();
        }
    }
}

/// <summary>UseBaseForge'ın hangi pipeline bileşenlerini ekleyeceğini belirleyen işaret nesnesi.</summary>
internal sealed class BaseForgeFeatures
{
    /// <summary>JWT kimlik doğrulama etkin mi?</summary>
    public bool JwtEnabled { get; init; }
}
