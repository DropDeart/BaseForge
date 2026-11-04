using System.ComponentModel.DataAnnotations;
using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Accounts;

/// <summary>
/// E-posta zorunlu değil (yalnız telefonla açılan hesap), ama verildiyse geçerli ve benzersiz olmalı.
/// ASP.NET Identity'nin RequireUniqueEmail kuralının yerini alır.
/// </summary>
public sealed class OptionalEmailValidator : IUserValidator<ApplicationUser>
{
    private static readonly EmailAddressAttribute EmailFormat = new();

    /// <inheritdoc />
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        var email = await manager.GetEmailAsync(user);
        if (string.IsNullOrWhiteSpace(email))
        {
            return IdentityResult.Success;
        }

        if (!EmailFormat.IsValid(email))
        {
            return IdentityResult.Failed(manager.ErrorDescriber.InvalidEmail(email));
        }

        var owner = await manager.FindByEmailAsync(email);
        return owner is not null && owner.Id != user.Id
            ? IdentityResult.Failed(manager.ErrorDescriber.DuplicateEmail(email))
            : IdentityResult.Success;
    }
}
