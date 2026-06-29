using BaseForge.CodeGen.Generation;
using BaseForge.CodeGen.Spec;

namespace BaseForge.CodeGen.Cli;

/// <summary>BaseForge kod üretici komut satırı arayüzü.</summary>
internal static class CliRunner
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0];
        var options = ParseOptions(args.Skip(1));

        try
        {
            return command switch
            {
                "er" => RunEr(options),
                "new-service" => RunNewService(options),
                "new-identity" => RunNewIdentity(options),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            return 1;
        }
    }

    private static int RunNewIdentity(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("spec", out var specPath) || string.IsNullOrWhiteSpace(specPath))
        {
            Console.Error.WriteLine("'--spec <auth.yaml>' zorunludur.");
            return 1;
        }

        var spec = SpecLoader.Load<AuthSpec>(specPath);
        if (string.IsNullOrWhiteSpace(spec.Service) || string.IsNullOrWhiteSpace(spec.Database))
        {
            Console.Error.WriteLine("auth.yaml: 'service' ve 'database' zorunludur.");
            return 1;
        }

        var output = options.GetValueOrDefault("output", $"./{spec.Service}");
        var files = IdentityGenerator.Generate(spec, output);

        Console.WriteLine($"{files.Count} dosya üretildi ({Path.GetFullPath(output)}):");
        foreach (var file in files)
        {
            Console.WriteLine($"  + {Path.GetRelativePath(output, file)}");
        }

        Console.WriteLine();
        Console.WriteLine("Çalıştırma:  cd \"" + output + "\" && docker compose up --build -d");
        Console.WriteLine("Secret'ları .env.example -> .env'e taşıyabilirsiniz.");
        return 0;
    }

    private static int RunEr(Dictionary<string, string> options)
    {
        var spec = LoadValidated(options, out var error);
        if (spec is null)
        {
            return error;
        }

        var output = options.GetValueOrDefault("output", ".");
        var path = WriteErDiagram(spec, output);
        Console.WriteLine($"ER diyagramı üretildi: {path}");
        Console.WriteLine("diagrams.net (draw.io) ile açabilirsiniz.");
        return 0;
    }

    private static int RunNewService(Dictionary<string, string> options)
    {
        var spec = LoadValidated(options, out var error);
        if (spec is null)
        {
            return error;
        }

        var output = options.GetValueOrDefault("output", $"./{spec.Service}");

        var erPath = WriteErDiagram(spec, output);
        Console.WriteLine($"ER diyagramı üretildi: {erPath}");
        Console.WriteLine("Lütfen ER diyagramını inceleyin (diagrams.net).");
        Console.WriteLine();

        if (!Confirm(options))
        {
            Console.WriteLine("İptal edildi. Kod üretilmedi.");
            return 0;
        }

        var files = CodeGenerator.Generate(spec, output);
        Console.WriteLine();
        Console.WriteLine($"{files.Count} dosya üretildi ({Path.GetFullPath(output)}):");
        foreach (var file in files)
        {
            Console.WriteLine($"  + {Path.GetRelativePath(output, file)}");
        }

        return 0;
    }

    private static ServiceSpec? LoadValidated(Dictionary<string, string> options, out int errorCode)
    {
        errorCode = 0;
        if (!options.TryGetValue("spec", out var specPath) || string.IsNullOrWhiteSpace(specPath))
        {
            Console.Error.WriteLine("'--spec <dosya.yaml>' zorunludur.");
            errorCode = 1;
            return null;
        }

        var spec = SpecLoader.Load(specPath);
        var errors = SpecValidator.Validate(spec);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine("Spec geçersiz:");
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            errorCode = 1;
            return null;
        }

        return spec;
    }

    private static string WriteErDiagram(ServiceSpec spec, string outputDir)
    {
        var docsDir = Path.Combine(outputDir, "docs");
        Directory.CreateDirectory(docsDir);
        var path = Path.Combine(docsDir, $"{spec.Service}.er.drawio");
        File.WriteAllText(path, DrawioErGenerator.Generate(spec));
        return Path.GetFullPath(path);
    }

    private static bool Confirm(Dictionary<string, string> options)
    {
        if (options.ContainsKey("yes"))
        {
            return true;
        }

        Console.Write("Kod üretilsin mi? (e/h): ");
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "e", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "evet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = list[i + 1];
                i++;
            }
            else
            {
                options[key] = "true"; // bayrak (örn. --er-only, --yes)
            }
        }

        return options;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: '{command}'.");
        PrintUsage();
        return 1;
    }

    private static bool IsHelp(string arg)
        => arg is "-h" or "--help" or "help";

    private static void PrintUsage()
    {
        Console.WriteLine("BaseForge kod üretici (baseforge)");
        Console.WriteLine();
        Console.WriteLine("Kullanım:");
        Console.WriteLine("  baseforge er           --spec <dosya.yaml> [--output <klasör>]");
        Console.WriteLine("  baseforge new-service  --spec <dosya.yaml> [--output <klasör>] [--yes]");
        Console.WriteLine("  baseforge new-identity --spec <auth.yaml>  [--output <klasör>]");
        Console.WriteLine();
        Console.WriteLine("Komutlar:");
        Console.WriteLine("  er            Spec'ten yalnızca draw.io ER diyagramı üretir.");
        Console.WriteLine("  new-service   ER üretir, onay alır ve servis iskelesini üretir.");
        Console.WriteLine("  new-identity  auth.yaml'dan config-driven merkez auth (Identity) servisi üretir.");
        Console.WriteLine();
        Console.WriteLine("Seçenekler:");
        Console.WriteLine("  --spec    YAML servis spec dosyası (zorunlu).");
        Console.WriteLine("  --output  Çıktı klasörü (er: '.', new-service: './<servis>').");
        Console.WriteLine("  --yes     Onay sormadan devam et.");
    }
}
