using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BaseForge.Identity.Data;

/// <summary>İlk açılışta OpenIddict scope/client'larını ve admin kullanıcıyı <see cref="AuthOptions"/>'tan oluşturur.</summary>
public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services, AuthOptions auth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auth);
        await SeedScopesAsync(services, auth, cancellationToken);
        await SeedClientsAsync(services, auth, cancellationToken);
        await SeedAdminAsync(services, auth);
    }

    private static async Task SeedScopesAsync(IServiceProvider services, AuthOptions auth, CancellationToken ct)
    {
        var manager = services.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var scope in auth.Scopes)
        {
            if (string.IsNullOrWhiteSpace(scope.Name) || await manager.FindByNameAsync(scope.Name, ct) is not null)
            {
                continue;
            }

            var descriptor = new OpenIddictScopeDescriptor { Name = scope.Name, DisplayName = scope.Name };
            if (!string.IsNullOrWhiteSpace(scope.Resource))
            {
                descriptor.Resources.Add(scope.Resource);
            }

            await manager.CreateAsync(descriptor, ct);
        }
    }

    private static async Task SeedClientsAsync(IServiceProvider services, AuthOptions auth, CancellationToken ct)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in auth.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId) || await manager.FindByClientIdAsync(client.ClientId, ct) is not null)
            {
                continue;
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientType = client.Public ? ClientTypes.Public : ClientTypes.Confidential,
                DisplayName = client.ClientId,
            };

            if (!client.Public && !string.IsNullOrWhiteSpace(client.Secret))
            {
                descriptor.ClientSecret = client.Secret;
            }

            descriptor.Permissions.Add(Permissions.Endpoints.Token);

            foreach (var grant in client.Grants)
            {
                switch (grant.Trim().ToLowerInvariant())
                {
                    case "password":
                        descriptor.Permissions.Add(Permissions.GrantTypes.Password);
                        break;
                    case "client_credentials":
                        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
                        break;
                    case "refresh_token":
                        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                        break;
                    case "authorization_code":
                        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
                        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
                        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
                        break;
                    default:
                        break;
                }
            }

            foreach (var scope in client.Scopes)
            {
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            }

            foreach (var uri in client.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }

            await manager.CreateAsync(descriptor, ct);
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider services, AuthOptions auth)
    {
        if (auth.SeedAdmin is null || string.IsNullOrWhiteSpace(auth.SeedAdmin.Email))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByNameAsync(auth.SeedAdmin.Email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = auth.SeedAdmin.Email,
            Email = auth.SeedAdmin.Email,
            EmailConfirmed = true,
            FullName = "Administrator",
        };
        await userManager.CreateAsync(admin, auth.SeedAdmin.Password);
    }
}
