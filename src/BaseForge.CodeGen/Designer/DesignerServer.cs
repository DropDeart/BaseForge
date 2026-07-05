using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace BaseForge.CodeGen.Designer;

/// <summary>
/// <c>baseforge new &lt;servis&gt;</c> komutunun web arayüzü host'u. Gömülü React dist'ini
/// (<c>web/</c> logical name'li resource'lar) sunar, <c>/api/*</c> uçlarını bağlar ve tarayıcıyı açar.
/// </summary>
internal static class DesignerServer
{
    public static int Run(string serviceName, int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Tek oturumluk çalışma dizinini (çıktı kökü) ve seed servis adını paylaş.
        builder.Services.AddSingleton(new DesignerContext(serviceName, Directory.GetCurrentDirectory()));

        // Spec sınıfları PascalCase property'li; React tarafıyla camelCase üzerinden konuşulur.
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
            o.SerializerOptions.DictionaryKeyPolicy = null; // entity/prop adları aynen korunur
        });

        var app = builder.Build();

        var webFiles = EmbeddedWebFileProvider.TryCreate(typeof(DesignerServer).Assembly);

        if (webFiles is not null)
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = webFiles });
        }

        DesignerEndpoints.Map(app);

        // SPA fallback: /api dışı ve dosya olmayan tüm yolları index.html'e yönlendir.
        if (webFiles is not null)
        {
            app.MapFallback(async context =>
            {
                var index = webFiles.GetFileInfo("index.html");
                if (index.Exists)
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.SendFileAsync(index);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });
        }

        var url = $"http://localhost:{port}";
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"BaseForge Designer çalışıyor: {url}");
            Console.WriteLine("Kapatmak için: Ctrl+C");
            if (webFiles is null)
            {
                Console.WriteLine("(Uyarı: gömülü arayüz bulunamadı — yalnızca /api uçları aktif. " +
                    "Release build ile React arayüzü gömülür.)");
            }

            OpenBrowser(url);
        });

        app.Run();
        return 0;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tarayıcı otomatik açılamadı ({ex.Message}). Elle açın: {url}");
        }
    }
}

/// <summary>Designer oturumu boyunca paylaşılan bağlam: seed servis adı ve çıktı kök dizini.</summary>
internal sealed record DesignerContext(string ServiceName, string WorkingDirectory);

/// <summary>
/// <c>web/</c> önekli gömülü resource'ları statik dosya olarak sunar. MSBuild target'ı dist
/// dosyalarını <c>web/&lt;yol&gt;</c> logical name'iyle gömer; burada ters/düz eğik çizgi
/// farkları normalize edilir.
/// </summary>
internal sealed class EmbeddedWebFileProvider(Assembly assembly, IReadOnlyDictionary<string, string> map)
    : IFileProvider
{
    private const string Prefix = "web/";

    /// <summary>Gömülü <c>web/</c> resource'ları varsa provider döndürür; yoksa <see langword="null"/>.</summary>
    public static EmbeddedWebFileProvider? TryCreate(Assembly assembly)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var normalized = name.Replace('\\', '/');
            if (normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                map[normalized[Prefix.Length..]] = name; // "assets/index.js" -> gerçek resource adı
            }
        }

        return map.Count > 0 ? new EmbeddedWebFileProvider(assembly, map) : null;
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var key = subpath.Replace('\\', '/').TrimStart('/');
        return map.TryGetValue(key, out var resource)
            ? new EmbeddedFile(assembly, resource, Path.GetFileName(key))
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private sealed class EmbeddedFile(Assembly assembly, string resourceName, string name) : IFileInfo
    {
        public bool Exists => true;
        public bool IsDirectory => false;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public string Name => name;
        public string PhysicalPath => null!;

        public long Length
        {
            get
            {
                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                return stream.Length;
            }
        }

        public Stream CreateReadStream() => assembly.GetManifestResourceStream(resourceName)!;
    }
}
