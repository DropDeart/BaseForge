using System.Security.Claims;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BaseForge.Identity.Controllers;

/// <summary>OpenIddict token endpoint'i: password, client_credentials ve refresh_token akışları.</summary>
[ApiController]
public sealed class AuthorizationController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthorizationController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict isteği çözümlenemedi.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordAsync(request);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return HandleClientCredentials(request);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshAsync();
        }

        return Forbidden("Desteklenmeyen grant türü.");
    }

    private async Task<IActionResult> HandlePasswordAsync(OpenIddictRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username ?? string.Empty);
        if (user is null)
        {
            return Forbidden("Kullanıcı adı veya parola hatalı.");
        }

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password ?? string.Empty, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            return Forbidden("Kullanıcı adı veya parola hatalı.");
        }

        var principal = await CreateUserPrincipalAsync(user, request.GetScopes());
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult HandleClientCredentials(OpenIddictRequest request)
    {
        // İstemci (client_id/secret) OpenIddict tarafından zaten doğrulandı.
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters_AuthType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(Claims.Subject, request.ClientId ?? string.Empty);
        identity.AddClaim(Claims.Name, request.ClientId ?? string.Empty);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(SeedData.ApiResource);
        SetDestinations(principal);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshAsync()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = result.Principal is null ? null : await _userManager.GetUserAsync(result.Principal);
        if (user is null)
        {
            return Forbidden("Refresh token geçersiz.");
        }

        var principal = await CreateUserPrincipalAsync(user, result.Principal!.GetScopes());
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreateUserPrincipalAsync(ApplicationUser user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters_AuthType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(Claims.Subject, user.Id.ToString());
        identity.AddClaim(Claims.Name, user.UserName ?? string.Empty);
        identity.AddClaim(Claims.Email, user.Email ?? string.Empty);

        foreach (var role in await _userManager.GetRolesAsync(user))
        {
            identity.AddClaim(Claims.Role, role);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(SeedData.ApiResource);
        SetDestinations(principal);
        return principal;
    }

    /// <summary>Her claim'in hangi token'a (access/id) yazılacağını belirler.</summary>
    private static void SetDestinations(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            var destinations = new List<string> { Destinations.AccessToken };

            // Profil/email/rol claim'leri id_token'a da yazılsın (ilgili scope verildiyse).
            if (claim.Type is Claims.Name or Claims.Email or Claims.Role)
            {
                destinations.Add(Destinations.IdentityToken);
            }

            claim.SetDestinations(destinations);
        }
    }

    private const string TokenValidationParameters_AuthType = "BaseForgeIdentity";

    private IActionResult Forbidden(string description)
    {
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });

        return Forbid(properties, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
