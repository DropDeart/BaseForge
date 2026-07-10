using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Entities;

/// <summary>Uygulama kullanıcısı (Guid anahtarlı). İleride custom alanlar buraya eklenir.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Kullanıcının görünen adı.</summary>
    public string? FullName { get; set; }

    /// <summary>Profil resmi — yüklenen dosyanın yolu (/uploads/avatars/...) ya da dış sağlayıcıdan (Google vb.) gelen URL.</summary>
    public string? AvatarUrl { get; set; }
}
