using System.Threading.Channels;
using RabbitMQ.Client;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// Tek bir <see cref="IConnection"/>'ı lazy olarak açıp yaşam boyu paylaşır (singleton). Kısa ömürlü
/// çağıranlar (örn. <c>RabbitMqPublisher</c>) için sınırlı bir kanal havuzu sağlar
/// (<see cref="RentChannelAsync"/>/<see cref="ReturnChannelAsync"/>) — uzun ömürlü çağıranlar
/// (örn. tüketici hosted service'i, kendi kanalını uygulama ömrü boyunca tutar) havuza ihtiyaç
/// duymaz, doğrudan <see cref="CreateChannelAsync"/> kullanmaya devam eder.
/// </summary>
public sealed class RabbitMqConnectionManager : IAsyncDisposable
{
    private const int ChannelPoolCapacity = 10;

    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Channel<IChannel> _channelPool = Channel.CreateBounded<IChannel>(ChannelPoolCapacity);
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

    /// <summary>
    /// Havuzdan bir kanal kiralar (varsa açık olan pooled bir kanalı geri kullanır, yoksa/kapalıysa
    /// yeni açar). Kiralanan kanal işiniz bitince MUTLAKA <see cref="ReturnChannelAsync"/> ile geri
    /// verilmelidir (<c>try/finally</c>) — aksi halde havuza dönmez, sızar. Bir kanal aynı anda
    /// yalnızca TEK bir çağıran tarafından kullanılmalıdır (<see cref="IChannel"/> thread-safe
    /// değildir); rent/return bunu garanti eder — havuzdaki bir kanal asla iki çağırana aynı anda
    /// verilmez.
    /// </summary>
    /// <param name="cancellationToken">İptal token'ı.</param>
    public async Task<IChannel> RentChannelAsync(CancellationToken cancellationToken = default)
    {
        while (_channelPool.Reader.TryRead(out var pooled))
        {
            if (pooled.IsOpen)
            {
                return pooled;
            }

            await pooled.DisposeAsync().ConfigureAwait(false);
        }

        return await CreateChannelAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="RentChannelAsync"/> ile kiralanan bir kanalı havuza iade eder. Kanal kapalıysa
    /// veya havuz doluysa (kapasite: <see cref="ChannelPoolCapacity"/>) dispose edilir.
    /// </summary>
    /// <param name="channel">İade edilecek kanal.</param>
    public async ValueTask ReturnChannelAsync(IChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!channel.IsOpen || !_channelPool.Writer.TryWrite(channel))
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }
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
        _channelPool.Writer.TryComplete();
        while (_channelPool.Reader.TryRead(out var pooled))
        {
            await pooled.DisposeAsync().ConfigureAwait(false);
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connectionLock.Dispose();
    }
}
