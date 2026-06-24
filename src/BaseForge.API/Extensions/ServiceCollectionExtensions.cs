using System.Text;
using BaseForge.API.Authentication;
using BaseForge.Core.Interfaces;
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

        // 4) JWT kimlik doğrulama
        if (options.Jwt is not null)
        {
            AddJwtAuthentication(services, options.Jwt);
        }

        return services;
    }

    private static void AddJwtAuthentication(IServiceCollection services, JwtOptions jwt)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization();
    }
}
