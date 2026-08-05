using AspNet.Security.OAuth.Apple;
using BaseForge.Identity.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Authentication;

/// <summary>Yapılandırmadaki (ClientId dolu) dış kimlik sağlayıcılarını kaydeder.</summary>
internal static class ExternalProviders
{
    public static void Add(AuthenticationBuilder builder, ProvidersOptions providers)
    {
        if (HasValue(providers.Google))
        {
            builder.AddGoogle(options =>
            {
                options.ClientId = providers.Google!.ClientId;
                options.ClientSecret = providers.Google.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                // Profil fotoğrafı: ilk girişte ApplicationUser.AvatarUrl'e kopyalanır (bkz. AccountApiController).
                options.ClaimActions.MapJsonKey("picture", "picture");
            });
        }

        if (HasValue(providers.GitHub))
        {
            builder.AddGitHub(options =>
            {
                options.ClientId = providers.GitHub!.ClientId;
                options.ClientSecret = providers.GitHub.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        if (HasValue(providers.Microsoft))
        {
            builder.AddMicrosoftAccount(options =>
            {
                options.ClientId = providers.Microsoft!.ClientId;
                options.ClientSecret = providers.Microsoft.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        if (HasValue(providers.Facebook))
        {
            builder.AddFacebook(options =>
            {
                options.AppId = providers.Facebook!.ClientId;
                options.AppSecret = providers.Facebook.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        if (HasAppleValue(providers.Apple))
        {
            builder.AddApple(options =>
            {
                options.ClientId = providers.Apple!.ClientId;
                options.TeamId = providers.Apple.TeamId;
                options.KeyId = providers.Apple.KeyId;
                // Apple'da statik bir client secret yok — paket, TeamId/KeyId/PrivateKey'den her
                // seferinde imzalanmış bir JWT üretir (bkz. AppleProviderOptions.PrivateKey doc'u).
                options.GenerateClientSecret = true;
                var privateKey = providers.Apple.PrivateKey.Replace("\\n", "\n", StringComparison.Ordinal);
                options.PrivateKey = (_, _) => Task.FromResult<ReadOnlyMemory<char>>(privateKey.AsMemory());
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }
    }

    private static bool HasValue(ExternalProviderOptions? provider) =>
        provider is not null && !string.IsNullOrWhiteSpace(provider.ClientId);

    private static bool HasAppleValue(AppleProviderOptions? provider) =>
        provider is not null
        && !string.IsNullOrWhiteSpace(provider.ClientId)
        && !string.IsNullOrWhiteSpace(provider.TeamId)
        && !string.IsNullOrWhiteSpace(provider.KeyId)
        && !string.IsNullOrWhiteSpace(provider.PrivateKey);
}
