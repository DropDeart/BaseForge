using RabbitMQ.Client;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// Tek bir <see cref="IConnection"/>'ı lazy olarak açıp yaşam boyu paylaşır (singleton).
/// Kanallar ise thread-safety için her çağrıda ayrı açılır (<see cref="CreateChannelAsync"/>) —
/// v1 basitliği; yüksek trafikte kanal havuzu ayrı bir iyileştirme konusudur.
/// </summary>
public sealed class RabbitMqConnectionManager : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    /// <summary>Verilen ayarlarla bir bağlantı yöneticisi oluşturur.</summary>
    /// <param name="options">RabbitMQ bağlantı ayarları.</param>
    public RabbitMqConnectionManager(RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Paylaşılan bağlantı üzerinde yeni bir kanal açar (bağlantı henüz yoksa önce kurar).</summary>
    /// <param name="cancellationToken">İptal token'ı.</param>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connectionLock.Dispose();
    }
}
