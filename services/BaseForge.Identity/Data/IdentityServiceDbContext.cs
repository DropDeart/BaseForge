using BaseForge.Identity.Accounts;
using BaseForge.Identity.Entities;
using BaseForge.Identity.Otp;
using BaseForge.Identity.Profile;
using BaseForge.Identity.Tenancy;
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
        UserProfile.Configure(builder);
        TenancyModel.Configure(builder);
        OtpModel.Configure(builder);
        AccountModel.Configure(builder);
    }
}
