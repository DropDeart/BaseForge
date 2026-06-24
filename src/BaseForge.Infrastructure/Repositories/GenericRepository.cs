using BaseForge.Core.Entities;
using BaseForge.Core.Interfaces;
using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Infrastructure.Repositories;

/// <summary>
/// <see cref="IRepository{TEntity, TKey}"/> sözleşmesinin EF Core implementasyonu.
/// Değişiklikler change tracker'a alınır; veritabanına yazma <see cref="IUnitOfWork"/>
/// üzerinden yapılır. Silme işlemleri <see cref="ISoftDelete"/> entity'lerde mantıksaldır.
/// </summary>
/// <typeparam name="TEntity">Yönetilen entity tipi.</typeparam>
/// <typeparam name="TKey">Entity'nin birincil anahtar tipi.</typeparam>
public class GenericRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>Bu repository'nin üzerinde çalıştığı context.</summary>
    protected BaseForgeDbContext Context { get; }

    /// <summary>Yönetilen entity'nin <see cref="DbSet{TEntity}"/>'i.</summary>
    protected DbSet<TEntity> Set { get; }

    /// <summary>Verilen context ile yeni bir repository oluşturur.</summary>
    /// <param name="context">EF Core context'i.</param>
    public GenericRepository(BaseForgeDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        Set = context.Set<TEntity>();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        => await Set.FindAsync([id], cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // Fiziksel Remove; ISoftDelete ise DbContext bunu mantıksal silmeye çevirir.
        Set.Remove(entity);
        return Task.CompletedTask;
    }
}

/// <summary>
/// <see cref="Guid"/> anahtarlı entity'ler için <see cref="GenericRepository{TEntity, TKey}"/> kısayolu.
/// </summary>
/// <typeparam name="TEntity"><see cref="BaseEntity"/>'den türeyen entity tipi.</typeparam>
public class GenericRepository<TEntity> : GenericRepository<TEntity, Guid>, IRepository<TEntity>
    where TEntity : BaseEntity
{
    /// <summary>Verilen context ile yeni bir repository oluşturur.</summary>
    /// <param name="context">EF Core context'i.</param>
    public GenericRepository(BaseForgeDbContext context)
        : base(context)
    {
    }
}
