using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Accounts;

/// <summary>Yeni hesaba rol verme: varsayılan rol + uygulamanın seçtiği izinli rol.</summary>
public static class RegistrationRoles
{
    /// <summary>Kullanıcı bu rolü kendine ekleyebilir mi (auth.yaml registration.selfAssignableRoles)?</summary>
    public static bool IsSelfAssignable(RegistrationOptions registration, string? role)
        => role is not null && registration.SelfAssignableRoles.Contains(role, StringComparer.Ordinal);

    /// <summary>Varsayılan rolü ve (selfAssignableRoles içindeyse) istenen rolü ekler.</summary>
    public static async Task AssignAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, RegistrationOptions registration, string? requestedRole)
    {
        await userManager.AddToRoleAsync(user, registration.DefaultRole);
        if (requestedRole != registration.DefaultRole && IsSelfAssignable(registration, requestedRole))
        {
            await userManager.AddToRoleAsync(user, requestedRole!);
        }
    }
}
