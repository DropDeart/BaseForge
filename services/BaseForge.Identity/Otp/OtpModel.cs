using Microsoft.EntityFrameworkCore;

namespace BaseForge.Identity.Otp;

/// <summary>OtpChallenges tablosunun EF eşlemesi ve var olan veritabanına eklenmesi.</summary>
public static class OtpModel
{
    /// <summary>EF modeli.</summary>
    public static void Configure(ModelBuilder builder)
        => builder.Entity<OtpChallenge>(otp =>
        {
            otp.ToTable("OtpChallenges");
            otp.Property(o => o.Channel).HasConversion<string>().HasMaxLength(10);
            otp.Property(o => o.Purpose).HasConversion<string>().HasMaxLength(20);
            otp.Property(o => o.Target).HasMaxLength(256);
            otp.Property(o => o.CodeHash).HasMaxLength(128);
            otp.HasIndex(o => new { o.Target, o.Purpose, o.CreatedAt });
        });

    /// <summary>0.6.3 ve öncesinden gelen identity_db için idempotent DDL.</summary>
    public static Task EnsureTableAsync(DbContext db, CancellationToken cancellationToken = default)
        => db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "OtpChallenges" (
                "Id" uuid NOT NULL CONSTRAINT "PK_OtpChallenges" PRIMARY KEY,
                "Channel" character varying(10) NOT NULL,
                "Target" character varying(256) NOT NULL,
                "Purpose" character varying(20) NOT NULL,
                "CodeHash" character varying(128) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "Attempts" integer NOT NULL,
                "ConsumedAt" timestamp with time zone NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_OtpChallenges_Target_Purpose_CreatedAt" ON "OtpChallenges" ("Target", "Purpose", "CreatedAt");
            """, cancellationToken);
}
