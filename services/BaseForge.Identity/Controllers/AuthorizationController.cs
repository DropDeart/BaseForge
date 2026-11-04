using System.Security.Claims;
using BaseForge.Identity.Data;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Otp;
using BaseForge.Identity.Profile;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BaseForge.Identity.Controllers;

/// <summary>OpenIddict token endpoint'i: password, client_credentials, authorization_code ve refresh_token akışları.</summary>
[ApiController]
public sealed class AuthorizationController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ITenantTokenResolver _tenantResolver;
    private readonly IMembershipService _memberships;
    private readonly MultiTenancyOptions _tenancy;
    private readonly AuthOptions _authOptions;
    private readonly IContactVerificationService _verification;

    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        ITenantTokenResolver tenantResolver,
        IMembershipService memberships,
        IContactVerificationService verification,
        AuthOptions authOptions)
    {
        _verification = verification;
        _authOptions = authOptions;
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
        _tenantResolver = tenantResolver;
        _memberships = memberships;
        _tenancy = authOptions.MultiTenancy;
    }

    /// <summary>Aktif şirketi seçen istek parametresi (password, refresh_token, authorize).</summary>
    private const string TenantParameter = "tenant_id";

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict isteği çözümlenemedi.");

        // Kullanıcı giriş yapmış mı? (interaktif cookie)
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (result?.Succeeded != true)
        {
            // Giriş yoksa login sayfasına 302 ile yönlendir (sonra buraya geri döner).
            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList());
            return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var user = await _userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("Oturum açan kullanıcı bulunamadı.");

        return await SignInUserAsync(user, request.GetScopes(), RequestedTenant(request), previousTenantId: null);
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

        if (request.GrantType == OtpRegistration.GrantType)
        {
            return await HandleOtpAsync(request);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsAsync(request);
        }

        if (request.IsRefreshTokenGrantType() || request.IsAuthorizationCodeGrantType())
        {
            // Her iki grant'ta da OpenIddict, kodun/refresh token'ın arkasındaki principal'ı
            // AuthenticateAsync ile kendisi çözer (Authorize()'da SignIn edilen ticket'tan) — burada
            // yalnızca aynı kullanıcı için yeniden SignIn yapılır, akışlar arasında fark yoktur.
            return await HandleServerManagedGrantAsync();
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

        return await SignInUserAsync(user, request.GetScopes(), RequestedTenant(request), previousTenantId: null);
    }

    /// <summary>Telefon + SMS kodu: doğrulanmış telefonun sahibi ya da (kayıt açıksa) yeni hesap.</summary>
    private async Task<IActionResult> HandleOtpAsync(OpenIddictRequest request)
    {
        if (!_authOptions.Otp.Enabled)
        {
            return Forbidden("Telefonla giriş kapalı.");
        }

        ApplicationUser user;
        try
        {
            user = await _verification.SignInWithPhoneAsync(
                (string?)request.GetParameter("phone") ?? string.Empty,
                (string?)request.GetParameter("code") ?? string.Empty,
                (string?)request.GetParameter("role"),
                HttpContext.RequestAborted);
        }
        catch (IdentityException ex)
        {
            return Forbidden(ex.Message);
        }

        return await SignInUserAsync(user, request.GetScopes(), RequestedTenant(request), previousTenantId: null);
    }

    private async Task<IActionResult> HandleClientCredentialsAsync(OpenIddictRequest request)
    {
        // İstemci (client_id/secret) OpenIddict tarafından zaten doğrulandı.
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters_AuthType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(Claims.Subject, request.ClientId ?? string.Empty);
        identity.AddClaim(Claims.Name, request.ClientId ?? string.Empty);

        // Servis token'ı işareti (BaseForge.Core.MultiTenancy.BaseForgeClaims.ServiceToken): servisler yalnızca bu
        // claim'i taşıyan çağrılarda x-tenant-id metadata'sına güvenir (bkz. docs/ARCH.md §5.1.1).
        identity.AddClaim("bf_svc", "1");

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(await ResolveResourcesAsync(principal.GetScopes()));
        SetDestinations(principal);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleServerManagedGrantAsync()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = result.Principal is null ? null : await _userManager.GetUserAsync(result.Principal);
        if (user is null)
        {
            return Forbidden("Yetkilendirme kodu ya da refresh token geçersiz.");
        }

        // Refresh'te şirket korunur; tenant_id parametresiyle başka şirkete geçilir.
        var request = HttpContext.GetOpenIddictServerRequest();
        return await SignInUserAsync(user, result.Principal!.GetScopes(),
            request is null ? null : RequestedTenant(request), result.Principal!.GetClaim(TenantParameter));
    }

    private static string? RequestedTenant(OpenIddictRequest request) => (string?)request.GetParameter(TenantParameter);

    private async Task<IActionResult> SignInUserAsync(ApplicationUser user, IEnumerable<string> scopes, string? requestedTenantId, string? previousTenantId)
    {
        await _memberships.ClaimInvitationsAsync(user, HttpContext.RequestAborted);
        var selection = await _tenantResolver.ResolveAsync(user.Id, requestedTenantId, previousTenantId, HttpContext.RequestAborted);
        if (selection.Error is not null)
        {
            return Forbidden(selection.Error);
        }

        var principal = await CreateUserPrincipalAsync(user, scopes, selection.Tenant);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreateUserPrincipalAsync(ApplicationUser user, IEnumerable<string> scopes, ActiveTenant? tenant)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters_AuthType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(Claims.Subject, user.Id.ToString());
        identity.AddClaim(Claims.Name, user.UserName ?? string.Empty);
        identity.AddClaim(Claims.Email, user.Email ?? string.Empty);

        // Şirket içi roller yalnızca aktif şirketin üyeliğinden gelir; global atanmış olsa da yazılmaz.
        var memberRoles = _tenancy.Enabled ? _tenancy.AllMemberRoles : [];
        var roles = (await _userManager.GetRolesAsync(user)).Where(r => !memberRoles.Contains(r, StringComparer.Ordinal));
        if (tenant is not null)
        {
            identity.AddClaim(TenantParameter, tenant.TenantId.ToString());
            identity.AddClaim("tenant_name", tenant.Name);
            roles = roles.Concat(tenant.Roles);
        }

        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            identity.AddClaim(Claims.Role, role);
        }

        // auth.yaml userProfile 'inToken: true' alanları (token yenilenene kadar eski değeri taşır).
        foreach (var (type, value) in UserProfile.TokenClaims(user))
        {
            identity.AddClaim(type, value);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(await ResolveResourcesAsync(principal.GetScopes()));
        SetDestinations(principal);
        return principal;
    }

    /// <summary>İstenen scope'lara karşılık gelen kaynakları (audience) config'teki scope tanımlarından çözer.</summary>
    private async Task<List<string>> ResolveResourcesAsync(IEnumerable<string> scopes)
    {
        var resources = new List<string>();
        await foreach (var resource in _scopeManager.ListResourcesAsync([.. scopes]))
        {
            resources.Add(resource);
        }

        return resources;
    }

    /// <summary>Her claim'in hangi token'a (access/id) yazılacağını belirler.</summary>
    private static void SetDestinations(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            var destinations = new List<string> { Destinations.AccessToken };

            // Profil/email/rol claim'leri id_token'a da yazılsın (ilgili scope verildiyse).
            if (claim.Type is Claims.Name or Claims.Email or Claims.Role or TenantParameter or "tenant_name" || UserProfile.Fields.Any(f => f.InToken && f.Name == claim.Type))
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
