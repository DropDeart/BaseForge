using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// Üretilen bir servisi arka planda ayağa kaldırır/durdurur — TAM docker compose stack'ini
/// (<c>docker compose up --build -d --wait</c>) kullanır, yalnızca postgres'i değil.
/// </summary>
/// <remarks>
/// Başlangıçta yalnızca postgres'i container'da, servisi yerel <c>dotnet run</c> ile çalıştırmak
/// denendi — ama üretilen <c>appsettings.json</c>'daki Kestrel bind adresleri (<c>http://+:8080</c>/
/// <c>:8081</c>) bilerek container-içi sabit tutuluyor (host mapping'i <c>DockerPorts</c> ile
/// değişir). Yerelde <c>dotnet run</c> ile çalıştırıldığında Kestrel bu SABİT portu doğrudan HOST'ta
/// dinlemeye çalışıyor ve host'taki başka bir uygulamayla çakışabiliyor (canlı testte tespit edildi).
/// Tam Docker kullanmak bu sorunu ortadan kaldırır (Kestrel her zaman container-izole 8080/8081'de
/// dinler) ve "Durdur" için de <c>docker compose down</c> yeterli olur — .NET tarafında process-tree
/// kill yönetmeye (ve olası orphan process'lere) gerek kalmaz.
/// </remarks>
internal static class RunRunner
{
    public sealed record StartResult(bool Success, string DockerOutput);

    private static readonly ConcurrentDictionary<string, byte> Started = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<StartResult> StartAsync(string outputDir, CancellationToken ct = default)
    {
        var (success, output) = await RunProcessAsync("docker", "compose up --build -d --wait", outputDir, ct);
        if (success)
        {
            Started[Normalize(outputDir)] = 0;
        }

        return new StartResult(success, output);
    }

    public static async Task<bool> StopAsync(string outputDir, CancellationToken ct = default)
    {
        var (success, _) = await RunProcessAsync("docker", "compose down", outputDir, ct);
        Started.TryRemove(Normalize(outputDir), out _);
        return success;
    }

    /// <summary>Designer kapanırken izlenen tüm stack'leri durdurur (orphan container kalmasın).</summary>
    public static async Task StopAllAsync()
    {
        foreach (var dir in Started.Keys.ToList())
        {
            await StopAsync(dir);
        }
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static async Task<(bool Success, string Output)> RunProcessAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        return (process.ExitCode == 0, output.ToString().Trim());
    }
}
