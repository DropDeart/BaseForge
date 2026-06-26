using Microsoft.AspNetCore.Identity;

namespace BaseForge.Identity.Entities;

/// <summary>Uygulama kullanıcısı (Guid anahtarlı). İleride custom alanlar buraya eklenir.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Kullanıcının görünen adı.</summary>
    public string? FullName { get; set; }
}
