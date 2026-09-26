using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Entities;

/// <summary>
/// Uygulama kullanıcısı (Guid anahtarlı). Domain'e özgü alanlar auth.yaml <c>userProfile</c>'dan üretilen
/// <c>ApplicationUser.Profile.cs</c> partial'ındadır (bkz. BaseForge docs/ARCH.md §6.3).
/// </summary>
public sealed partial class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Kullanıcının görünen adı.</summary>
    public string? FullName { get; set; }

    /// <summary>Profil resmi — yüklenen dosyanın yolu (/uploads/avatars/...) ya da dış sağlayıcıdan (Google vb.) gelen URL.</summary>
    public string? AvatarUrl { get; set; }
}
