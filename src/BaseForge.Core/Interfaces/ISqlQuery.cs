namespace BaseForge.Core.Interfaces;

/// <summary>
/// Ham SQL ile okuma/çalıştırma için soyutlama. Implementasyonu (Dapper tabanlı)
/// <c>BaseForge.Infrastructure</c> katmanındadır ve aktif <see cref="IUnitOfWork"/>
/// bağlantısı/transaction'ı üzerinde çalışır. Karmaşık join ve rapor sorgularında kullanılır.
/// </summary>
/// <remarks>
/// SQL elle yazıldığı için parametreler her zaman <c>parameters</c> argümanıyla geçilmeli
/// (SQL injection'a karşı). Soft delete koşulu (<c>is_deleted = false</c>) sorguya elle eklenmelidir.
/// </remarks>
public interface ISqlQuery
{
    /// <summary>SQL'i çalıştırır ve sonuç satırlarını <typeparamref name="T"/> nesnelerine map eder.</summary>
    /// <typeparam name="T">Satır başına oluşturulacak hedef tip.</typeparam>
    /// <param name="sql">Çalıştırılacak SQL ifadesi.</param>
    /// <param name="parameters">Parametre nesnesi (örn. <c>new { id }</c>); yoksa <see langword="null"/>.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>SQL'i çalıştırır ve tek bir sonuç döndürür; satır yoksa <see langword="default"/>.</summary>
    /// <typeparam name="T">Hedef tip.</typeparam>
    /// <param name="sql">Çalıştırılacak SQL ifadesi.</param>
    /// <param name="parameters">Parametre nesnesi; yoksa <see langword="null"/>.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Sonuç döndürmeyen bir SQL (DML) çalıştırır ve etkilenen satır sayısını döndürür.</summary>
    /// <param name="sql">Çalıştırılacak SQL ifadesi.</param>
    /// <param name="parameters">Parametre nesnesi; yoksa <see langword="null"/>.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}
