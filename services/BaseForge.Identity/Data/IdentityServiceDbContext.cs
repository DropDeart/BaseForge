using BaseForge.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Data;

/// <summary>
/// ASP.NET Core Identity + OpenIddict tablolarını barındıran EF Core context'i (identity_db).
/// </summary>
public sealed class IdentityServiceDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
