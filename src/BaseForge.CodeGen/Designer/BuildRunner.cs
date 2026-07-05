using System.Diagnostics;
using System.Text;

namespace BaseForge.CodeGen.Designer;

/// <summary>Üretilen servis dizininde <c>dotnet build</c> çalıştırır ve sonucu özetler.</summary>
internal static class BuildRunner
{
    public sealed record Result(bool Success, string Output);

    public static async Task<Result> BuildAsync(string projectDir, CancellationToken ct = default)
    {
        if (!Directory.Exists(projectDir))
        {
            return new Result(false, $"Dizin bulunamadı: {projectDir}");
        }

        var psi = new ProcessStartInfo("dotnet", $"build \"{projectDir}\" -c Debug --nologo")
        {
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

        return new Result(process.ExitCode == 0, Tail(output.ToString(), 60));
    }

    /// <summary>Çıktının son <paramref name="lines"/> satırını döndürür (UI'da özet göstermek için).</summary>
    private static string Tail(string text, int lines)
    {
        var all = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return all.Length <= lines
            ? text.Trim()
            : string.Join('\n', all[^lines..]).Trim();
    }
}
