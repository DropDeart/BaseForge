using BaseForge.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Accounts;

/// <summary>Kullanıcı tablosuna eklenen index'ler (telefonla giriş ve davet eşleşmesi).</summary>
public static class AccountModel
{
    private const string PhoneIndex = "IX_AspNetUsers_PhoneNumber_Confirmed";

    /// <summary>EF modeli.</summary>
    public static void Configure(ModelBuilder builder)
        => builder.Entity<ApplicationUser>()
            .HasIndex(u => u.PhoneNumber)
            .HasDatabaseName(PhoneIndex)
            .HasFilter("\"PhoneNumberConfirmed\"");

    /// <summary>Var olan identity_db için idempotent DDL.</summary>
    public static Task EnsureIndexesAsync(DbContext db, CancellationToken cancellationToken = default)
        => db.Database.ExecuteSqlRawAsync(
            $"""CREATE INDEX IF NOT EXISTS "{PhoneIndex}" ON "AspNetUsers" ("PhoneNumber") WHERE "PhoneNumberConfirmed";""",
            cancellationToken);
}
