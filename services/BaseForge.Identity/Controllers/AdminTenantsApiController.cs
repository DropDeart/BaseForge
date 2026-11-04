using BaseForge.Identity.Accounts;
using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BaseForge.Identity.Controllers;

/// <summary>Platform yöneticisinin şirket onayı ve askısı.</summary>
[ApiController]
[Route("api/admin/tenants")]
[RequireFeature(AuthFeature.MultiTenancy)]
[Authorize(Policy = TenancyRegistration.ApproverPolicy)]
public sealed class AdminTenantsApiController(UserManager<ApplicationUser> userManager, ITenantOnboardingService onboarding)
    : ControllerBase
{
    /// <summary>Şirketler (status ile süzülür, örn. ?status=Pending).</summary>
    [HttpGet]
    public Task<IReadOnlyList<TenantDto>> List([FromQuery] TenantStatus? status, CancellationToken cancellationToken)
        => onboarding.ListAsync(status, cancellationToken);

    /// <summary>Onaylar.</summary>
    [HttpPost("{tenantId:guid}/approve")]
    public Task<TenantDto> Approve(Guid tenantId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReviewTenantRequest? request, CancellationToken cancellationToken)
        => ReviewAsync(tenantId, TenantStatus.Active, request, cancellationToken);

    /// <summary>Reddeder.</summary>
    [HttpPost("{tenantId:guid}/reject")]
    public Task<TenantDto> Reject(Guid tenantId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReviewTenantRequest? request, CancellationToken cancellationToken)
        => ReviewAsync(tenantId, TenantStatus.Rejected, request, cancellationToken);

    /// <summary>Askıya alır (üyeler o şirket için token alamaz).</summary>
    [HttpPost("{tenantId:guid}/suspend")]
    public Task<TenantDto> Suspend(Guid tenantId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReviewTenantRequest? request, CancellationToken cancellationToken)
        => ReviewAsync(tenantId, TenantStatus.Suspended, request, cancellationToken);

    /// <summary>Askıdan çıkarır.</summary>
    [HttpPost("{tenantId:guid}/activate")]
    public Task<TenantDto> Activate(Guid tenantId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReviewTenantRequest? request, CancellationToken cancellationToken)
        => ReviewAsync(tenantId, TenantStatus.Active, request, cancellationToken);

    private Task<TenantDto> ReviewAsync(Guid tenantId, TenantStatus target, ReviewTenantRequest? request, CancellationToken cancellationToken)
    {
        return onboarding.ReviewAsync(tenantId, target, userManager.GetRequiredUserId(User), request?.Note, cancellationToken);
    }
}
