using BaseForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BaseForge.Infrastructure.Messaging;

/// <summary>
/// Transactional outbox relay'i: periyodik olarak <see cref="BaseForgeDbContext.OutboxMessages"/>'ı
/// tarayıp henüz işlenmemiş (<c>ProcessedAt IS NULL</c>) satırları <see cref="IRabbitMqPublisher"/>
/// ile gerçek RabbitMQ broker'ına yazar. Bu, <c>OutboxEventBus</c>'ın DB'ye yazdığı olaylarla
/// broker'a giden mesajlar arasındaki tek bağlantıdır; çalışmıyorsa hiçbir olay yayınlanmaz.
/// </summary>
/// <remarks>
/// Ölçeklenmiş (çok replikalı) dağıtımlarda birden fazla instance aynı anda çalışabilir: satır
/// seçimi <c>FOR UPDATE SKIP LOCKED</c> ile yapılır, böylece iki instance aynı satırı asla aynı anda
/// işlemez (ikincisi kilitli satırları atlar, beklemez) — ekstra bir lease/claim kolonu gerekmez.
/// Teslim garantisi <b>at-least-once</b>'tır: publish RabbitMQ'ya gittikten sonra, <c>ProcessedAt</c>
/// commit edilmeden bu process çökerse mesaj bir sonraki taramada tekrar gönderilebilir (v1
/// sınırlaması, bkz. docs/ARCH.md §5.2). Başarısız mesajlar <see cref="RabbitMqOptions.OutboxMaxRetries"/>'e
/// kadar tekrar denenir; bu sınır aşılırsa satır <c>IsDead</c> olarak işaretlenip taramadan çıkarılır
/// (silinmez — manuel inceleme için kalır). İşlenmiş satırlar <see cref="RabbitMqOptions.OutboxRetention"/>
/// süresinden eskiyse periyodik olarak (bkz. <see cref="CleanupInterval"/>) toplu silinir — her
/// tarama tick'inde değil, çünkü retention zaten günler mertebesinde bir pencere; publish taramasının
/// hızlı kadansına (varsayılan 2sn) bağlamak gereksiz bir DELETE round-trip'i olurdu.
/// </remarks>
public sealed class OutboxPublisherHostedService : BackgroundService
{
    /// <summary>Retention temizliğinin en sık ne kadar aralıkla çalışacağı — publish taramasından bağımsız bir kadans.</summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxPublisherHostedService> _logger;
    private DateTimeOffset _nextCleanupAtUtc = DateTimeOffset.MinValue;

    /// <summary>Verilen bağımlılıklarla bir outbox relay hosted service'i oluşturur.</summary>
    public OutboxPublisherHostedService(
        IServiceScopeFactory scopeFactory,
        RabbitMqOptions options,
        ILogger<OutboxPublisherHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.OutboxPollingInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Bir sonraki tick'te tekrar denenir; tek bir taramanın (örn. DB bağlantı hatası)
                // arka plan servisinin tamamen durmasına yol açmaması gerekir.
                _logger.LogError(ex, "Outbox taraması başarısız oldu, bir sonraki tick'te tekrar denenecek.");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BaseForgeDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        await using (var transaction = await context.Database.BeginTransactionAsync(stoppingToken).ConfigureAwait(false))
        {
            var batch = await context.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM "OutboxMessages"
                    WHERE "ProcessedAt" IS NULL AND "IsDead" = false
                    ORDER BY "OccurredAt"
                    LIMIT {_options.OutboxBatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(stoppingToken).ConfigureAwait(false);

            foreach (var message in batch)
            {
                try
                {
                    await publisher.PublishRawAsync(message.EventType, message.Payload, stoppingToken).ConfigureAwait(false);
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    message.Error = null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Satır seviyesinde izole hata: bu satırın başarısız olması batch'teki diğer
                    // satırların işlenmesini/commit'ini engellemez (aksi halde başarıyla publish
                    // edilmiş bir mesaj rollback yüzünden bir sonraki tick'te tekrar gönderilirdi).
                    message.RetryCount++;
                    message.Error = ex.Message;

                    if (message.RetryCount >= _options.OutboxMaxRetries)
                    {
                        message.IsDead = true;
                        _logger.LogError(ex, "Outbox mesajı {RetryCount} kez denendi ve dead olarak işaretlendi, artık denenmeyecek: {EventType} ({EventId}).",
                            message.RetryCount, message.EventType, message.EventId);
                    }
                    else
                    {
                        _logger.LogWarning(ex, "Outbox mesajı publish edilemedi: {EventType} ({EventId}), deneme {RetryCount}.",
                            message.EventType, message.EventId, message.RetryCount);
                    }
                }
            }

            if (batch.Count > 0)
            {
                await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(stoppingToken).ConfigureAwait(false);
        }

        await CleanupIfDueAsync(context, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// İşlenmiş (<c>ProcessedAt</c> dolu) ve <see cref="RabbitMqOptions.OutboxRetention"/>'dan eski
    /// satırları toplu siler — ama yalnızca <see cref="CleanupInterval"/> geçtiyse (publish taramasının
    /// her tick'inde değil). Yukarıdaki batch transaction'ından KASITLI olarak ayrı/sonra çalışır —
    /// retention temizliği ile publish batch'inin satır kilitleri (<c>FOR UPDATE SKIP LOCKED</c>) aynı
    /// transaction'ı paylaşmasın diye.
    /// </summary>
    private async Task CleanupIfDueAsync(BaseForgeDbContext context, CancellationToken stoppingToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _nextCleanupAtUtc)
        {
            return;
        }

        _nextCleanupAtUtc = now + CleanupInterval;

        var cutoff = now - _options.OutboxRetention;
        await context.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);
    }
}
