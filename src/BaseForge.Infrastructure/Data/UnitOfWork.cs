using BaseForge.Core.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace BaseForge.Infrastructure.Data;

/// <summary>
/// <see cref="IUnitOfWork"/>'in EF Core implementasyonu. Aynı scope'taki tüm
/// repository'ler ve <see cref="ISqlQuery"/> bu context'i (ve transaction'ı) paylaşır.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BaseForgeDbContext _context;
    private IDbContextTransaction? _transaction;

    /// <summary>Verilen context üzerinde yeni bir iş birimi oluşturur.</summary>
    /// <param name="context">EF Core context'i.</param>
    public UnitOfWork(BaseForgeDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }
}
