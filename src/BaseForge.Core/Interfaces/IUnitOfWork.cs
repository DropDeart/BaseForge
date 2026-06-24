namespace BaseForge.Core.Interfaces;

/// <summary>
/// Bir iş birimi (Unit of Work) boyunca veritabanı işlemlerini (transaction) yönetir.
/// ADO.NET implementasyonu <c>BaseForge.Infrastructure</c> katmanında yer alır.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Bekleyen tüm değişiklikleri veritabanına yazar ve etkilenen satır sayısını döndürür.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Yeni bir veritabanı transaction'ı başlatır.</summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Açık transaction'ı kalıcı hale getirir (commit).</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Açık transaction'ı geri alır (rollback).</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
