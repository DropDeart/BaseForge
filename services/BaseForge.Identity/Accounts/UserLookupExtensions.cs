using System.Security.Claims;
using BaseForge.Identity.Entities;
using BaseForge.Identity.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Accounts;

/// <summary>Kullanıcı bulma yardımcıları.</summary>
public static class UserLookupExtensions
{
    /// <summary>İsteği yapan kullanıcının kaydı; yoksa 403.</summary>
    public static async Task<ApplicationUser> GetRequiredUserAsync(this UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        => await userManager.GetUserAsync(principal) ?? throw IdentityException.Forbidden("Kullanıcı bulunamadı.");

    /// <summary>İsteği yapan kullanıcının kimliği (sub claim'i; veritabanına gitmez); yoksa 403.</summary>
    public static Guid GetRequiredUserId(this UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        => Guid.TryParse(userManager.GetUserId(principal), out var id) ? id : throw IdentityException.Forbidden("Kullanıcı bulunamadı.");

    /// <summary>Numarayı doğrulamış kullanıcı (doğrulanmış numara tek hesaba aittir).</summary>
    public static Task<ApplicationUser?> FindByConfirmedPhoneAsync(this IQueryable<ApplicationUser> users, string phone, CancellationToken cancellationToken)
        => users.FirstOrDefaultAsync(u => u.PhoneNumber == phone && u.PhoneNumberConfirmed, cancellationToken);
}
