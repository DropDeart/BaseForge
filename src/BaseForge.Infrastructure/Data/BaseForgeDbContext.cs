using System.Linq.Expressions;
using BaseForge.Core.Entities;
using BaseForge.Core.Interfaces;
using BaseForge.Core.Messaging;
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
    private readonly ICurrentTenant? _currentTenant;

    /// <summary>Yeni bir <see cref="BaseForgeDbContext"/> oluşturur.</summary>
    /// <param name="options">EF Core context seçenekleri.</param>
    /// <param name="currentUser">Audit (<c>CreatedBy</c>) için o anki kullanıcı; DI'da kayıtlı değilse <see langword="null"/>.</param>
    /// <param name="currentTenant">
    /// Multi-tenancy (<c>TenantId</c> damgalama + query filter) için o anki kiracı; DI'da kayıtlı
    /// değilse (<c>EnableMultiTenancy()</c> çağrılmadıysa) <see langword="null"/>.
    /// </param>
    protected BaseForgeDbContext(DbContextOptions options, ICurrentUser? currentUser = null, ICurrentTenant? currentTenant = null)
        : base(options)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// O anki kiracının kimliği — <see cref="OnModelCreating"/>'de kurulan query filter'ların
    /// referans aldığı instance member (EF Core'un "current tenant" desenindeki <c>this.X</c>
    /// referansıyla birebir aynı mekanizma: filtre, model build anındaki değil, sorguyu çalıştıran
    /// DbContext instance'ının o anki değerini okur).
    /// </summary>
    private Guid? CurrentTenantId => _currentTenant?.TenantId;

    /// <summary>
    /// Transactional outbox tablosu — bir olay yayınlandığında (<c>OutboxEventBus.PublishAsync</c>)
    /// business entity değişikliğiyle AYNI <c>SaveChangesAsync</c> çağrısında yazılır. Her
    /// <c>BaseForgeDbContext</c>'ten türeyen (yani CodeGen'in ürettiği her) context bu DbSet'i
    /// otomatik miras alır.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Tüketici tarafı idempotency (Inbox pattern) tablosu — bir olayın bu serviste daha önce
    /// başarıyla işlendiğini işaretler (bkz. <see cref="InboxMessage"/>). Her
    /// <c>BaseForgeDbContext</c>'ten türeyen context bu DbSet'i otomatik miras alır.
    /// </summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

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

        // OutboxMessage'ın birincil anahtarı EventId'dir (EF'in "Id"/"{Tip}Id" adlandırma
        // konvansiyonuna uymadığı için açıkça belirtilmesi gerekir). Relay'in her tick'te taradığı
        // (ProcessedAt IS NULL AND IsDead = false) ve temizlik geçişinin sildiği (ProcessedAt dolu +
        // eski) satırları indeksli bir aralık taraması olarak bulabilmesi için birleşik bir indeks eklendi
        // — aksi halde tablo büyüdükçe her tick tam tablo taraması olurdu.
        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.HasKey(m => m.EventId);
            outbox.HasIndex(m => new { m.ProcessedAt, m.IsDead });
        });

        // ISoftDelete ve/veya ITenantEntity uygulayan entity'lere birleşik bir global query filter
        // eklenir. EF Core her entity tipi için yalnızca TEK bir query filter'a izin verdiğinden,
        // iki koşul (varsa) tek bir Expression.AndAlso ile birleştirilir.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isSoftDelete = typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType);
            var isTenant = typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType);

            if (!isSoftDelete && !isTenant)
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression? body = null;

            if (isSoftDelete)
            {
                var isDeleted = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                body = Expression.Not(isDeleted);
            }

            if (isTenant)
            {
                var tenantId = Expression.Convert(
                    Expression.Property(parameter, nameof(ITenantEntity.TenantId)), typeof(Guid?));
                // Constant'ı açıkça BaseForgeDbContext olarak tiple: CurrentTenantId private ve yalnızca
                // burada tanımlı — Expression.Constant(this) runtime tipini (türetilmiş context sınıfını)
                // kullanırsa reflection bu private üyeyi türetilmiş tipte bulamaz (private üyeler
                // FlattenHierarchy ile miras alınmaz).
                var currentTenantId = Expression.Property(
                    Expression.Constant(this, typeof(BaseForgeDbContext)), nameof(CurrentTenantId));
                var tenantMatches = Expression.Equal(tenantId, currentTenantId);
                body = body is null ? tenantMatches : Expression.AndAlso(body, tenantMatches);
            }

            entityType.SetQueryFilter(Expression.Lambda(body!, parameter));
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

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            entry.Entity.TenantId = _currentTenant?.TenantId
                ?? throw new InvalidOperationException(
                    $"'{entry.Entity.GetType().Name}' bir ITenantEntity ama o anki istekte çözümlenebilir bir " +
                    "TenantId yok (ICurrentTenant.TenantId null). Multi-tenant bir serviste JWT'de 'tenant_id' " +
                    "claim'i bulunmalı.");
        }
    }
}
