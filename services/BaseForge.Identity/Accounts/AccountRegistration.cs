using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Accounts;

/// <summary>Hesap servisleri: dış giriş, telefon/e-posta doğrulama, isteğe bağlı e-posta kuralı.</summary>
public static class AccountRegistration
{
    /// <summary>Servis kayıtları.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        // RequireUniqueEmail yerine: e-posta zorunlu değil, verildiyse benzersiz.
        services.AddScoped<IUserValidator<ApplicationUser>, OptionalEmailValidator>();
        services.AddScoped<IExternalAccountService, ExternalAccountService>();
        services.AddScoped<IContactVerificationService, ContactVerificationService>();
        return services;
    }
}
