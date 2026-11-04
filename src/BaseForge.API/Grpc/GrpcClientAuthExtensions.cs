using BaseForge.API.Authentication;
using BaseForge.Core.MultiTenancy;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

// DI genişletmesi: üretilen Program.cs ek using olmadan çağırabilsin diye bu isim alanında.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Üretilen gRPC istemcilerine kimlik bilgisi ekler (bkz. docs/ARCH.md §5.1.1):
/// <list type="bullet">
/// <item>Bir HTTP isteğinin içindeyse gelen <c>Authorization</c> başlığı olduğu gibi iletilir — kullanıcının
/// kimliği, rolleri ve <c>tenant_id</c>'si karşı servise taşınır.</item>
/// <item>Aksi halde (RabbitMQ tüketicisi, arka plan işi) <see cref="IServiceTokenProvider"/>'dan servis token'ı alınır
/// ve ortam kiracısı (<see cref="TenantScope"/>) <c>x-tenant-id</c> metadata'sıyla gönderilir.</item>
/// </list>
/// </summary>
public static class GrpcClientAuthExtensions
{
    /// <summary>gRPC istemcisine BaseForge çağrı kimlik bilgilerini ekler (h2c kanallarda da çalışır).</summary>
    public static IHttpClientBuilder AddBaseForgeCallCredentials(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .AddCallCredentials(ApplyAsync)
            // Container içi düz HTTP/2 (h2c): call credentials varsayılan olarak yalnızca TLS'te gönderilir.
            .ConfigureChannel(o => o.UnsafeUseInsecureChannelCallCredentials = true);
    }

    private static async Task ApplyAsync(AuthInterceptorContext context, Metadata metadata, IServiceProvider services)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var incoming = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming) && incoming.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            metadata.Add("authorization", incoming);

            // Gelen çağrı zaten bir servis token'ıysa (zincirleme servis çağrısı) kiracı başlığını da taşı.
            var forwardedTenant = httpContext!.Request.Headers[BaseForgeClaims.TenantIdHeader].ToString();
            if (httpContext.User.HasClaim(BaseForgeClaims.ServiceToken, "1") && forwardedTenant.Length > 0)
            {
                metadata.Add(BaseForgeClaims.TenantIdHeader, forwardedTenant);
            }

            return;
        }

        if (services.GetService<IServiceTokenProvider>() is not { } provider
            || await provider.GetTokenAsync(context.CancellationToken).ConfigureAwait(false) is not { } token)
        {
            return;
        }

        metadata.Add("authorization", "Bearer " + token);
        if (TenantScope.Current is { } tenantId)
        {
            metadata.Add(BaseForgeClaims.TenantIdHeader, tenantId.ToString());
        }
    }
}
