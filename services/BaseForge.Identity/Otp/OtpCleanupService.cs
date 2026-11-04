using BaseForge.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Otp;

/// <summary>Saatte bir, bir günden eski kodları siler (gönderim isteğinin yolunda değil).</summary>
internal sealed partial class OtpCleanupService(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<OtpCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cutoff = clock.GetUtcNow() - Retention;
                await scope.ServiceProvider.GetRequiredService<IdentityServiceDbContext>().Set<OtpChallenge>()
                    .Where(c => c.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCleanupFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Eski doğrulama kodları silinemedi; bir sonraki turda denenecek.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
