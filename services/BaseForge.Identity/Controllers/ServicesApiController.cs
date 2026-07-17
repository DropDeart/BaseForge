using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>
/// Dashboard'un "Servisler" bölümü için canlı durum uçları. Kayıtlı servis listesi
/// (<c>wwwroot/services.json</c>, bkz. BaseForge.CodeGen'deki <c>ServiceRegistry</c>) codegen anında
/// donmuş bir anlık görüntüdür — bu uç, her servisin kendi zaten var olan <c>/health</c> ucunu
/// <c>host.docker.internal</c> üzerinden yoklayarak canlılık bilgisini tamamlar. Her üretilen servis
/// kendi bağımsız docker-compose ağında çalıştığı için (container DNS'i paylaşılmıyor), mevcut
/// cross-service gRPC çağrılarıyla aynı desen kullanılır: host-mapped port + host.docker.internal.
/// </summary>
[ApiController]
[Route("api/services")]
public sealed class ServicesApiController : ControllerBase
{
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;

    public ServicesApiController(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var registryPath = Path.Combine(_environment.WebRootPath, "services.json");
        if (!System.IO.File.Exists(registryPath))
        {
            return Ok(Array.Empty<ServiceStatusRow>());
        }

        var json = await System.IO.File.ReadAllTextAsync(registryPath, ct);
        var registry = JsonSerializer.Deserialize<ServiceRegistryFile>(json, RegistryJsonOptions) ?? new ServiceRegistryFile([]);

        var client = _httpClientFactory.CreateClient("ServiceHealthClient");
        var checks = await Task.WhenAll(registry.Services
            .Where(s => !s.IsIdentity)
            .Select(s => CheckAsync(client, s, ct)));

        return Ok(checks);
    }

    private static async Task<ServiceStatusRow> CheckAsync(HttpClient client, ServiceRegistryEntry service, CancellationToken ct)
    {
        if (service.RestPort is not { } restPort)
        {
            return new ServiceStatusRow(service.Name, false);
        }

        try
        {
            var response = await client.GetAsync($"http://host.docker.internal:{restPort}/health", ct);
            return new ServiceStatusRow(service.Name, response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ServiceStatusRow(service.Name, false);
        }
    }

    private sealed record ServiceRegistryFile(List<ServiceRegistryEntry> Services);

    private sealed record ServiceRegistryEntry(
        string Name,
        int? RestPort,
        int? GrpcPort,
        int? PostgresPort,
        int? EntityCount,
        bool IsIdentity,
        string? Authority,
        string? Audience,
        bool Protected);

    private sealed record ServiceStatusRow(string Name, bool Healthy);
}
