using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.API.RateLimiting;

/// <summary>
/// BaseForge'un hazır rate limit politikaları. <c>AddBaseForge</c> her zaman kaydeder, <c>UseBaseForge</c>
/// middleware'i ekler; üretilen controller'lar politikaları adıyla <c>[EnableRateLimiting]</c> ile kullanır.
/// Bkz. docs/ARCH.md §6.1.
/// </summary>
public static class BaseForgeRateLimits
{
    /// <summary>Sayaç (<c>counters</c>) uçlarının politikası: istemci başına sabit pencere.</summary>
    public const string CounterPolicy = "baseforge-counter";

    /// <summary>Sayaç politikasında pencere başına izin verilen istek (varsayılan 30).</summary>
    public static int CounterPermitLimit { get; set; } = 30;

    /// <summary>Sayaç politikasının pencere süresi (varsayılan 1 dakika).</summary>
    public static TimeSpan CounterWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Politikaları kaydeder; aşımda 429 döner.</summary>
    internal static IServiceCollection AddBaseForgeRateLimits(this IServiceCollection services)
        => services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(CounterPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = CounterPermitLimit,
                    Window = CounterWindow,
                    QueueLimit = 0,
                }));
        });

    /// <summary>Giriş yapmış kullanıcı için <c>sub</c>, aksi halde istemci IP'si.</summary>
    private static string PartitionKey(HttpContext context)
        => context.User.FindFirstValue("sub") is { Length: > 0 } sub
            ? "u:" + sub
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
