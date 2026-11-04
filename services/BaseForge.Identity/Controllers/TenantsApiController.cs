using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>Kullanıcının şirketleri: açma, katılma, davet yanıtı, varsayılan, ayrılma.</summary>
[ApiController]
[Route("api/tenants")]
[RequireTenancy]
[Authorize(AuthenticationSchemes = TenancyRegistration.AuthSchemes)]
public sealed class TenantsApiController(
    UserManager<ApplicationUser> userManager,
    IMembershipService memberships,
    ITenantOnboardingService onboarding) : ControllerBase
{
    /// <summary>Üyelikler, davetler ve bekleyen istekler.</summary>
    [HttpGet("mine")]
    public async Task<IReadOnlyList<MyMembershipDto>> Mine(CancellationToken cancellationToken)
        => await memberships.ListMineAsync(await CurrentUserAsync(), cancellationToken);

    /// <summary>Şirket açar (onay gerekiyorsa beklemede başlar).</summary>
    [HttpPost]
    public async Task<MyMembershipDto> Create(CreateTenantRequest request, CancellationToken cancellationToken)
        => await onboarding.CreateAsync((await CurrentUserAsync()).Id, request, cancellationToken);

    /// <summary>Şirket kodundan ad (katılmadan önce).</summary>
    [HttpGet("lookup/{slug}")]
    public Task<TenantLookupDto> Lookup(string slug, CancellationToken cancellationToken)
        => memberships.LookupAsync(slug, cancellationToken);

    /// <summary>Katılma isteği gönderir.</summary>
    [HttpPost("join")]
    public async Task<MyMembershipDto> Join(JoinTenantRequest request, CancellationToken cancellationToken)
        => await memberships.RequestJoinAsync((await CurrentUserAsync()).Id, request.Slug, cancellationToken);

    /// <summary>Daveti kabul eder.</summary>
    [HttpPost("memberships/{membershipId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.AcceptAsync((await CurrentUserAsync()).Id, membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Daveti reddeder.</summary>
    [HttpPost("memberships/{membershipId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.DeclineAsync((await CurrentUserAsync()).Id, membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Girişte seçilecek şirketi belirler.</summary>
    [HttpPut("memberships/{membershipId:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.SetDefaultAsync((await CurrentUserAsync()).Id, membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Şirketten ayrılır ya da isteği geri çeker.</summary>
    [HttpDelete("memberships/{membershipId:guid}")]
    public async Task<IActionResult> Leave(Guid membershipId, CancellationToken cancellationToken)
    {
        await memberships.LeaveAsync((await CurrentUserAsync()).Id, membershipId, cancellationToken);
        return NoContent();
    }

    private async Task<ApplicationUser> CurrentUserAsync()
        => await userManager.GetUserAsync(User) ?? throw IdentityException.Forbidden("Kullanıcı bulunamadı.");
}
