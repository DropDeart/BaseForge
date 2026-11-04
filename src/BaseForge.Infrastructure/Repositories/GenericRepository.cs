using BaseForge.Core.CQRS;
using BaseForge.Core.Entities;
using BaseForge.Core.Exceptions;
using BaseForge.Core.Interfaces;
using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;

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
    public virtual async Task<(IReadOnlyList<TEntity> Items, int TotalCount)> ListPagedAsync(
        int skip,
        int take,
        string? sortBy,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? applyFilter = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = Set.AsNoTracking();
        if (applyFilter is not null)
        {
            query = applyFilter(query);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        query = string.IsNullOrWhiteSpace(sortBy)
            ? query.OrderByDescending(x => x.CreatedAt)
            : ApplyDynamicSort(query, sortBy);

        var items = await query.Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, totalCount);
    }

    /// <summary>
    /// Sıralanabilir alanlar: entity'nin skaler (değer tipi/string) public özellikleri; kiracı ve silinme
    /// bilgisi (<c>TenantId</c>, <c>IsDeleted</c>, <c>DeletedAt</c>) bilgi sızdırmaması için hariç.
    /// Üretilen handler'lar daha dar bir listeyle <see cref="SortExpression.Normalize"/>'ı zaten çağırır.
    /// </summary>
    private static readonly string[] SortableProperties = typeof(TEntity)
        .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string) || p.PropertyType.IsValueType)
        .Select(p => p.Name)
        .Where(n => n is not ("TenantId" or "IsDeleted" or "DeletedAt"))
        .ToArray();

    /// <summary>
    /// <paramref name="sortBy"/>'ı önce <see cref="SortExpression.Normalize"/> ile doğrular (yalnızca
    /// <c>Alan [asc|desc]</c> parçaları ve <see cref="SortableProperties"/>), sonra Dynamic LINQ ile uygular.
    /// Geçersiz ifade <see cref="ValidationException"/> (400) olur; serbest Dynamic LINQ ifadesi çalıştırılmaz.
    /// </summary>
    private static IQueryable<TEntity> ApplyDynamicSort(IQueryable<TEntity> query, string sortBy)
    {
        var normalized = SortExpression.Normalize(sortBy, SortableProperties);
        if (normalized is null)
        {
            return query.OrderByDescending(x => x.CreatedAt);
        }

        try
        {
            return query.OrderBy(normalized);
        }
        catch (Exception ex) when (ex is ParseException or InvalidOperationException)
        {
            throw new ValidationException("sortBy", $"Geçersiz sıralama ifadesi: '{sortBy}'.");
        }
    }

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
