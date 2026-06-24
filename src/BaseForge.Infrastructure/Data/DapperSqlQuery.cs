using BaseForge.Core.Interfaces;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BaseForge.Infrastructure.Data;

/// <summary>
/// <see cref="ISqlQuery"/>'nin Dapper implementasyonu. Sorguları, EF Core context'inin
/// <see cref="System.Data.Common.DbConnection"/>'ı ve (varsa) aktif transaction'ı üzerinde
/// çalıştırır; böylece EF ile aynı bağlantı/işlem bağlamı paylaşılır.
/// </summary>
public sealed class DapperSqlQuery : ISqlQuery
{
    private readonly BaseForgeDbContext _context;

    /// <summary>Verilen context üzerinden çalışan bir Dapper sorgu yardımcısı oluşturur.</summary>
    /// <param name="context">EF Core context'i.</param>
    public DapperSqlQuery(BaseForgeDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Database.GetDbConnection()
            .QueryAsync<T>(CreateCommand(sql, parameters, cancellationToken))
            .ConfigureAwait(false);
        return rows.AsList();
    }

    /// <inheritdoc />
    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
        => _context.Database.GetDbConnection()
            .QuerySingleOrDefaultAsync<T?>(CreateCommand(sql, parameters, cancellationToken));

    /// <inheritdoc />
    public Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
        => _context.Database.GetDbConnection()
            .ExecuteAsync(CreateCommand(sql, parameters, cancellationToken));

    private CommandDefinition CreateCommand(string sql, object? parameters, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        return new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
    }
}
