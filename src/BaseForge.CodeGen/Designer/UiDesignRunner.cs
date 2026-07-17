using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// Ayrı bir repo/tool olarak dağıtılan <c>uidesign</c> (BaseForge.UiDesigner.Cli) global dotnet tool'unu
/// bulur ve seçilen servislerle başlatır. BaseForge.CodeGen bu tool'a REFERANS VERMEZ (ayrı repo, ayrı
/// release döngüsü — bkz. mimari plan) — yalnızca PATH üzerinden bulunup bulunmadığı kontrol edilir ve
/// bulunursa alt process olarak, Designer'ın kendi ömründen bağımsız (detached) başlatılır.
/// </summary>
internal static class UiDesignRunner
{
    private const string ToolCommand = "uidesign";
    private const string InstallCommand = "dotnet tool install -g BaseForge.UiDesigner.Cli";

    public sealed record LaunchResult(bool Success, string? Url, string Message);

    /// <summary>Seçilen servislerle <c>uidesign design</c>'i başlatır; tool kurulu değilse kurulum talimatı döner.</summary>
    public static async Task<LaunchResult> LaunchAsync(string workspaceRoot, IReadOnlyList<string> services, CancellationToken ct)
    {
        if (!await IsToolAvailableAsync(ct))
        {
            return new LaunchResult(false, null, $"'{ToolCommand}' bulunamadı. Kurmak için: {InstallCommand}");
        }

        var port = FindFreePort();
        var servicesArg = string.Join(',', services);
        var psi = new ProcessStartInfo(ToolCommand, $"design --workspace \"{workspaceRoot}\" --services \"{servicesArg}\" --port {port}")
        {
            WorkingDirectory = workspaceRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            if (Process.Start(psi) is null)
            {
                return new LaunchResult(false, null, $"'{ToolCommand}' başlatılamadı.");
            }
        }
        catch (Win32Exception ex)
        {
            return new LaunchResult(false, null, $"'{ToolCommand}' başlatılamadı: {ex.Message}");
        }

        var url = $"http://localhost:{port}";
        var ready = await WaitForPortAsync(port, TimeSpan.FromSeconds(15), ct);
        return ready
            ? new LaunchResult(true, url, "UI Designer başlatıldı.")
            : new LaunchResult(true, url, "UI Designer başlatıldı ama henüz yanıt vermiyor — birkaç saniye sonra sayfayı yenileyin.");
    }

    private static async Task<bool> IsToolAvailableAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ToolCommand, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(ct);
            return true;
        }
        catch (Win32Exception)
        {
            // PATH üzerinde bulunamadı (tool kurulu değil).
            return false;
        }
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<bool> WaitForPortAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, ct);
                return true;
            }
            catch (SocketException)
            {
                await Task.Delay(300, ct);
            }
        }

        return false;
    }
}
