using BaseForge.Identity.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaseForge.Identity.Tenancy;

/// <summary>Kiracı yönetimi servis kayıtları.</summary>
public static class TenancyRegistration
{
    /// <summary>Cookie (Ortak Giriş SPA) ve Bearer (diğer uygulamalar) birlikte kabul edilir.</summary>
    public const string AuthSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore";

    /// <summary>Şirket onaylayan platform yöneticisi politikası.</summary>
    public const string ApproverPolicy = "tenancy.approver";

    /// <summary>Servisler ve onaylayan politikası (roller config'ten).</summary>
    public static IServiceCollection AddTenancy(this IServiceCollection services, AuthOptions options)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITenantAdminGuard, TenantAdminGuard>();
        services.AddScoped<ITenantOnboardingService, TenantOnboardingService>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<IMemberAdministrationService, MemberAdministrationService>();
        services.AddScoped<ITenantTokenResolver, TenantTokenResolver>();

        var approverRoles = options.MultiTenancy.ApproverRoles.Count > 0 ? options.MultiTenancy.ApproverRoles : ["SuperAdmin"];
        services.AddAuthorizationBuilder().AddPolicy(ApproverPolicy, policy => policy
            .AddAuthenticationSchemes(AuthSchemes.Split(','))
            .RequireRole(approverRoles));
        return services;
    }
}
