using BaseForge.Identity.Configuration;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Accounts;

/// <summary>Yeni hesaba rol verme: varsayılan rol + uygulamanın seçtiği izinli rol.</summary>
public static class RegistrationRoles
{
    /// <summary>Varsayılan rolü ve (selfAssignableRoles içindeyse) istenen rolü ekler.</summary>
    public static async Task AssignAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, RegistrationOptions registration, string? requestedRole)
    {
        await userManager.AddToRoleAsync(user, registration.DefaultRole);
        if (requestedRole is not null && requestedRole != registration.DefaultRole
            && registration.SelfAssignableRoles.Contains(requestedRole, StringComparer.Ordinal))
        {
            await userManager.AddToRoleAsync(user, requestedRole);
        }
    }
}
