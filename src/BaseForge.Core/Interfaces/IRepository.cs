using BaseForge.Core.Entities;

namespace BaseForge.Core.Interfaces;

/// <summary>
/// Bir entity tipi için temel veri erişim sözleşmesi. Implementasyon ADO.NET ile
/// <c>BaseForge.Infrastructure</c> katmanında yapılır. Silme işlemleri
/// <see cref="ISoftDelete"/> destekleyen entity'lerde mantıksaldır.
/// </summary>
/// <typeparam name="TEntity">Yönetilen entity tipi.</typeparam>
/// <typeparam name="TKey">Entity'nin birincil anahtar tipi.</typeparam>
public interface IRepository<TEntity, in TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>Anahtara göre tek bir kayıt getirir; bulunamazsa <see langword="null"/>.</summary>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>Silinmemiş tüm kayıtları getirir.</summary>
    Task<IReadOnlyList<TEntity>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Yeni bir kayıt ekler.</summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Var olan bir kaydı günceller.</summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Bir kaydı siler (entity <see cref="ISoftDelete"/> ise mantıksal silme).</summary>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="Guid"/> anahtarlı entity'ler için kısayol repository sözleşmesi.
/// </summary>
/// <typeparam name="TEntity"><see cref="BaseEntity"/>'den türeyen entity tipi.</typeparam>
public interface IRepository<TEntity> : IRepository<TEntity, Guid>
    where TEntity : BaseEntity;
