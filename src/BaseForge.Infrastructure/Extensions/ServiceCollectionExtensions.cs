using BaseForge.Core.Interfaces;
using BaseForge.Infrastructure.Data;
using BaseForge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.Infrastructure.Extensions;

/// <summary>
/// BaseForge altyapı servislerini DI konteynerine kaydeden extension metotları.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// PostgreSQL bağlantı dizesiyle BaseForge altyapısını kaydeder: <typeparamref name="TContext"/>
    /// (EF Core), <see cref="IRepository{TEntity, TKey}"/>/<see cref="IRepository{TEntity}"/>,
    /// <see cref="IUnitOfWork"/> ve <see cref="ISqlQuery"/> (Dapper).
    /// </summary>
    /// <typeparam name="TContext">Uygulamanın <see cref="BaseForgeDbContext"/>'ten türeyen context tipi.</typeparam>
    /// <param name="services">DI servis koleksiyonu.</param>
    /// <param name="connectionString">PostgreSQL bağlantı dizesi.</param>
    /// <returns>Zincirleme için aynı <paramref name="services"/>.</returns>
    public static IServiceCollection AddBaseForgeInfrastructure<TContext>(
        this IServiceCollection services,
        string connectionString)
        where TContext : BaseForgeDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddBaseForgeInfrastructure<TContext>(options => options.UseNpgsql(connectionString));
    }

    /// <summary>
    /// EF Core seçeneklerini elle yapılandırarak BaseForge altyapısını kaydeder.
    /// </summary>
    /// <typeparam name="TContext">Uygulamanın <see cref="BaseForgeDbContext"/>'ten türeyen context tipi.</typeparam>
    /// <param name="services">DI servis koleksiyonu.</param>
    /// <param name="configureOptions">EF Core context seçeneklerini yapılandıran delege.</param>
    /// <returns>Zincirleme için aynı <paramref name="services"/>.</returns>
    public static IServiceCollection AddBaseForgeInfrastructure<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
        where TContext : BaseForgeDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddDbContext<TContext>(configureOptions);

        // Repository/UnitOfWork/SqlQuery, somut TContext yerine base tip üzerinden çözülür.
        services.AddScoped<BaseForgeDbContext>(sp => sp.GetRequiredService<TContext>());

        services.AddScoped(typeof(IRepository<,>), typeof(GenericRepository<,>));
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISqlQuery, DapperSqlQuery>();

        return services;
    }
}
