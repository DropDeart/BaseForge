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
    }

    private static bool HasValue(ExternalProviderOptions? provider) =>
        provider is not null && !string.IsNullOrWhiteSpace(provider.ClientId);
}
