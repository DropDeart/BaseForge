using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>Kullanıcının şirketleri: açma, katılma, davet yanıtı, varsayılan, ayrılma.</summary>
[ApiController]
[Route("api/tenants")]
[RequireFeature(AuthFeature.MultiTenancy)]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
public sealed class TenantsApiController(
    UserManager<ApplicationUser> userManager,
    IMembershipService memberships,
    ITenantOnboardingService onboarding,
    IAuthorizationService authorization) : ControllerBase
{
    /// <summary>Üyelikler, davetler ve bekleyen istekler.</summary>
    [HttpGet("mine")]
    public async Task<IReadOnlyList<MyMembershipDto>> Mine(CancellationToken cancellationToken)
        => await memberships.ListMineAsync(await userManager.GetRequiredUserAsync(User), cancellationToken);

    /// <summary>Şirket açar (onay gerekiyorsa beklemede başlar).</summary>
    [HttpPost]
    public async Task<MyMembershipDto> Create(CreateTenantRequest request, CancellationToken cancellationToken)
        => await onboarding.CreateAsync(userManager.GetRequiredUserId(User), request, cancellationToken);

    /// <summary>Şirketin adını ve vergi numarasını günceller (şirket yöneticisi).</summary>
    [HttpPut("{tenantId:guid}")]
    public async Task<TenantDto> Update(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken)
        => await onboarding.UpdateAsync(tenantId, await authorization.GetTenantActorAsync(userManager, User), request, cancellationToken);

    /// <summary>Şirket kodundan ad (katılmadan önce).</summary>
    [HttpGet("lookup/{slug}")]
    public Task<TenantLookupDto> Lookup(string slug, CancellationToken cancellationToken)
        => memberships.LookupAsync(slug, cancellationToken);

    /// <summary>Katılma isteği gönderir.</summary>
    [HttpPost("join")]
    public async Task<MyMembershipDto> Join(JoinTenantRequest request, CancellationToken cancellationToken)
        => await memberships.RequestJoinAsync(userManager.GetRequiredUserId(User), request.Slug, cancellationToken);

    /// <summary>Daveti kabul eder.</summary>
    [HttpPost("memberships/{membershipId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.AcceptAsync(userManager.GetRequiredUserId(User), membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Daveti reddeder.</summary>
    [HttpPost("memberships/{membershipId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.DeclineAsync(userManager.GetRequiredUserId(User), membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Girişte seçilecek şirketi belirler.</summary>
    [HttpPut("memberships/{membershipId:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.SetDefaultAsync(userManager.GetRequiredUserId(User), membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Şirketten ayrılır ya da isteği geri çeker.</summary>
    [HttpDelete("memberships/{membershipId:guid}")]
    public async Task<IActionResult> Leave(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.LeaveAsync(userManager.GetRequiredUserId(User), membershipId, cancellationToken);
        return NoContent();
    }
}
