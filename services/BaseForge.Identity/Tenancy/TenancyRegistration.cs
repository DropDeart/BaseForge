using BaseForge.Identity.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaseForge.Identity.Tenancy;

/// <summary>Kiracı yönetimi servis kayıtları.</summary>
public static class TenancyRegistration
{
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

        // Roller boş olamaz (auth.yaml doğrulaması); şirket onayı ve üye yönetiminde platform yöneticisini bu politika belirler.
        services.AddAuthorizationBuilder().AddPolicy(ApproverPolicy, policy => policy
            .AddAuthenticationSchemes(AuthSchemes.CookieOrBearer.Split(','))
            .RequireRole(options.MultiTenancy.ApproverRoles));
        return services;
    }
}
