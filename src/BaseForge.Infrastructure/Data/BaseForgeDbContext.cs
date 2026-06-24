using System.Linq.Expressions;
using BaseForge.Core.Entities;
using BaseForge.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Infrastructure.Data;

/// <summary>
/// BaseForge tabanlı tüm uygulama <see cref="DbContext"/>'lerinin türeyeceği temel sınıf.
/// Audit alanlarını (<see cref="IAuditEntity"/>) otomatik doldurur, silme işlemlerini
/// <see cref="ISoftDelete"/> entity'lerde mantıksal silmeye çevirir ve silinmiş kayıtları
/// global query filter ile gizler.
/// </summary>
public abstract class BaseForgeDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    /// <summary>Yeni bir <see cref="BaseForgeDbContext"/> oluşturur.</summary>
    /// <param name="options">EF Core context seçenekleri.</param>
    /// <param name="currentUser">Audit (<c>CreatedBy</c>) için o anki kullanıcı; DI'da kayıtlı değilse <see langword="null"/>.</param>
    protected BaseForgeDbContext(DbContextOptions options, ICurrentUser? currentUser = null)
        : base(options) => _currentUser = currentUser;

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // ISoftDelete uygulayan tüm entity'lere "IsDeleted == false" global query filter'ı eklenir.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);
            entityType.SetQueryFilter(filter);
        }
    }

    /// <summary>
    /// Kaydetmeden önce change tracker'ı tarayarak audit alanlarını doldurur ve
    /// silinen <see cref="ISoftDelete"/> entity'lerini mantıksal silmeye çevirir.
    /// </summary>
    protected virtual void ApplyAuditAndSoftDelete()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= _currentUser?.UserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                default:
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }
    }
}
