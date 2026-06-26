using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BaseForge.Identity.Data;

/// <summary>İlk açılışta OpenIddict client'larını, scope'u ve bir admin kullanıcısını oluşturur.</summary>
public static class SeedData
{
    public const string ApiScope = "api";
    public const string ApiResource = "baseforge-api";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await SeedClientsAsync(services, cancellationToken);
        await SeedScopeAsync(services, cancellationToken);
        await SeedAdminUserAsync(services);
    }

    private static async Task SeedClientsAsync(IServiceProvider services, CancellationToken ct)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();

        // Servis-servis (makineler arası): client_id + secret ile token alır.
        if (await manager.FindByClientIdAsync("service-worker", ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "service-worker",
                ClientSecret = "service-secret",
                ClientType = ClientTypes.Confidential,
                DisplayName = "Service worker (machine-to-machine)",
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + ApiScope,
                },
            }, ct);
        }

        // Kullanıcı istemcisi (SPA): parola + refresh + (ileride) authorization_code.
        if (await manager.FindByClientIdAsync("spa-client", ct) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "spa-client",
                ClientType = ClientTypes.Public,
                DisplayName = "SPA / kullanıcı istemcisi",
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.Password,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.Prefixes.Scope + ApiScope,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                },
            }, ct);
        }
    }

    private static async Task SeedScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var manager = services.GetRequiredService<IOpenIddictScopeManager>();
        if (await manager.FindByNameAsync(ApiScope, ct) is null)
        {
            await manager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = ApiScope,
                DisplayName = "BaseForge API erişimi",
                Resources = { ApiResource },
            }, ct);
        }
    }

    private static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        const string email = "admin@baseforge.local";
        if (await userManager.FindByNameAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "BaseForge Admin",
        };
        await userManager.CreateAsync(admin, "Admin!2345");
    }
}
