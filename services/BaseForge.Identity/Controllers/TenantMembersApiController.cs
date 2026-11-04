using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaseForge.Identity.Controllers;

/// <summary>Şirket yöneticisinin üye işlemleri (platform yöneticisi de yapabilir).</summary>
[ApiController]
[Route("api/tenants/{tenantId:guid}/members")]
[RequireFeature(AuthFeature.MultiTenancy)]
[Authorize(AuthenticationSchemes = AuthSchemes.CookieOrBearer)]
public sealed class TenantMembersApiController(
    UserManager<ApplicationUser> userManager,
    IMemberAdministrationService members,
    IAuthorizationService authorization) : ControllerBase
{
    /// <summary>Üyeler, davetler, istekler.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<MemberDto>> List(Guid tenantId, CancellationToken cancellationToken)
        => await members.ListAsync(tenantId, await ActorAsync(), cancellationToken);

    /// <summary>E-posta veya telefonla davet eder.</summary>
    [HttpPost("invitations")]
    public async Task<MemberDto> Invite(Guid tenantId, InviteMemberRequest request, CancellationToken cancellationToken)
        => await members.InviteAsync(tenantId, await ActorAsync(), request, cancellationToken);

    /// <summary>Katılma isteğini onaylar.</summary>
    [HttpPost("{membershipId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid tenantId, Guid membershipId, MemberRolesRequest request, CancellationToken cancellationToken)
    {
        await members.ApproveAsync(tenantId, await ActorAsync(), membershipId, request.Roles, cancellationToken);
        return NoContent();
    }

    /// <summary>Katılma isteğini reddeder.</summary>
    [HttpPost("{membershipId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        await members.RejectAsync(tenantId, await ActorAsync(), membershipId, cancellationToken);
        return NoContent();
    }

    /// <summary>Rolleri değiştirir.</summary>
    [HttpPut("{membershipId:guid}/roles")]
    public async Task<IActionResult> ChangeRoles(Guid tenantId, Guid membershipId, MemberRolesRequest request, CancellationToken cancellationToken)
    {
        await members.ChangeRolesAsync(tenantId, await ActorAsync(), membershipId, request.Roles, cancellationToken);
        return NoContent();
    }

    /// <summary>Üyeyi çıkarır ya da daveti geri alır.</summary>
    [HttpDelete("{membershipId:guid}")]
    public async Task<IActionResult> Remove(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        await members.RemoveAsync(tenantId, await ActorAsync(), membershipId, cancellationToken);
        return NoContent();
    }

    // Platform yöneticisi = şirket onaylayan politika (üye olmadan da yönetebilir).
    private async Task<TenantActor> ActorAsync()
        => new(userManager.GetRequiredUserId(User),
            (await authorization.AuthorizeAsync(User, TenancyRegistration.ApproverPolicy)).Succeeded);
}
