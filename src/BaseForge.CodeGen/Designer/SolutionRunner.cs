using System.Diagnostics;
using System.Text;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// Üretilen servisi, kullanıcı isterse en yakın <c>.slnx</c>/<c>.sln</c> dosyasına ekler.
/// Kullanıcı istemezse servis diskte bağımsız bir klasör olarak kalır (örn. ayrı yayınlama/deploy için).
/// </summary>
internal static class SolutionRunner
{
    public sealed record Result(bool Success, string Output);

    /// <summary><paramref name="startDir"/>'den başlayıp kök dizine kadar yukarı çıkarak ilk
    /// <c>.slnx</c> (öncelikli) ya da <c>.sln</c> dosyasını bulur. Yoksa <see langword="null"/>.</summary>
    public static string? FindNearestSolution(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var found = dir.EnumerateFiles("*.slnx").Concat(dir.EnumerateFiles("*.sln")).FirstOrDefault();
            if (found is not null)
            {
                return found.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary><paramref name="csprojPath"/>'i <paramref name="solutionPath"/>'e, opsiyonel bir
    /// çözüm klasörü (<paramref name="solutionFolder"/>) altında ekler — <c>dotnet sln add</c> hem
    /// <c>.sln</c> hem <c>.slnx</c> ile çalışır.</summary>
    public static async Task<Result> AddProjectAsync(
        string solutionPath, string csprojPath, string? solutionFolder, CancellationToken ct = default)
    {
        if (!File.Exists(solutionPath))
        {
            return new Result(false, $"Solution bulunamadı: {solutionPath}");
        }

        if (!File.Exists(csprojPath))
        {
            return new Result(false, $"Proje dosyası bulunamadı: {csprojPath}");
        }

        var args = $"sln \"{solutionPath}\" add \"{csprojPath}\"";
        if (!string.IsNullOrWhiteSpace(solutionFolder))
        {
            args += $" -s \"{solutionFolder}\"";
        }

        var psi = new ProcessStartInfo("dotnet", args)
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

        return new Result(process.ExitCode == 0, output.ToString().Trim());
    }
}
