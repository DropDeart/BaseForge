using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BaseForge.API.Extensions;

namespace BaseForge.API.Authentication;

/// <summary>Servis-servis çağrıları için erişim token'ı sağlar (bkz. docs/ARCH.md §5.1.1).</summary>
public interface IServiceTokenProvider
{
    /// <summary>Geçerli bir servis token'ı; servis istemcisi yapılandırılmamışsa <see langword="null"/>.</summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Identity'nin token ucundan <c>client_credentials</c> ile token alır ve süresi dolmadan (60 sn pay) önbellekte tutar.
/// <see cref="JwtOptions.ServiceClientId"/>/<see cref="JwtOptions.ServiceClientSecret"/> boşsa hiç istek atmaz.
/// </summary>
internal sealed class ServiceTokenProvider : IServiceTokenProvider, IDisposable
{
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JwtOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public ServiceTokenProvider(IHttpClientFactory httpClientFactory, JwtOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ServiceClientId) && !string.IsNullOrWhiteSpace(_options.ServiceClientSecret)
        && !string.IsNullOrWhiteSpace(_options.Authority);

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _token;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _token;
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ServiceClientId!,
                ["client_secret"] = _options.ServiceClientSecret!,
                ["scope"] = _options.ServiceScope,
            };
            var endpoint = new Uri(new Uri(_options.Authority!.TrimEnd('/') + "/"), "connect/token");
            using var client = _httpClientFactory.CreateClient(nameof(ServiceTokenProvider));
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Identity token ucundan boş yanıt döndü.");
            _token = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresIn, 0)) - ExpirySkew;
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
