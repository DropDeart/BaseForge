namespace BaseForge.Core.Entities;

/// <summary>
/// Tüm entity'ler için temel sınıf. Tek tip bir kimlik (<typeparamref name="TKey"/>),
/// denetim (audit) alanları ve mantıksal silme desteği sağlar.
/// </summary>
/// <typeparam name="TKey">Birincil anahtar tipi (örn. <see cref="Guid"/>, <see cref="long"/>).</typeparam>
public abstract class BaseEntity<TKey> : IAuditEntity, ISoftDelete
    where TKey : notnull
{
    /// <summary>Entity'nin birincil anahtarı.</summary>
    public TKey Id { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// <see cref="Guid"/> anahtarlı entity'ler için kısayol temel sınıf.
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>;
