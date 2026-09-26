using Microsoft.Extensions.Hosting;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// Designer sekmesinin nabzı. Arayüz periyodik olarak <c>/api/heartbeat</c> çağırır; en az bir nabız alındıktan sonra
/// <see cref="Timeout"/> boyunca hiç gelmezse (sekme "Kapat"a basılmadan kapatıldı) sunucu kendini kapatır — port açık
/// kalıp bir sonraki <c>baseforge new</c>'de "hâlâ çalışıyor" görünmesin.
/// </summary>
internal sealed class DesignerHeartbeat
{
    /// <summary>
    /// Nabız kesilince kapanma süresi. Chrome arka plandaki sekmelerin zamanlayıcılarını dakikada bire kadar
    /// kıstığından, sayfa yenileme ve arka plan sekmesi yanlışlıkla kapatmasın diye birkaç dakika bırakılır.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private long _lastTicks;

    public void Touch() => Interlocked.Exchange(ref _lastTicks, DateTime.UtcNow.Ticks);

    /// <summary>En az bir nabız alındı ve son nabızdan bu yana <see cref="Timeout"/> geçti mi?</summary>
    public bool IsExpired()
    {
        var last = Interlocked.Read(ref _lastTicks);
        return last != 0 && DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) > Timeout;
    }

    /// <summary>Designer'ın başlattığı container'ları durdurup uygulamayı kapatır.</summary>
    public static async Task ShutdownAsync(IHostApplicationLifetime lifetime)
    {
        await RunRunner.StopAllAsync();
        lifetime.StopApplication();
    }
}

/// <summary>Nabız kesildiğinde Designer'ı kapatan arka plan görevi.</summary>
internal sealed class DesignerHeartbeatWatcher(DesignerHeartbeat heartbeat, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (heartbeat.IsExpired())
            {
                Console.WriteLine("Designer sekmesi kapatılmış görünüyor — Designer kapatılıyor.");
                await DesignerHeartbeat.ShutdownAsync(lifetime);
                return;
            }
        }
    }
}
