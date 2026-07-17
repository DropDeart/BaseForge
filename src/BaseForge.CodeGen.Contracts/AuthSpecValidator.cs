namespace BaseForge.CodeGen.Contracts;

/// <summary>Bir <see cref="AuthSpec"/>'i identity servisi üretiminden önce doğrular.</summary>
public static class AuthSpecValidator
{
    /// <summary>Spec'i doğrular ve bulunan hataların listesini döndürür (boşsa geçerli).</summary>
    public static IReadOnlyList<string> Validate(AuthSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(spec.Service))
        {
            errors.Add("'service' alanı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(spec.Database))
        {
            errors.Add("'database' alanı zorunludur.");
        }

        if (spec.SeedAdmin is not null && !string.IsNullOrEmpty(spec.SeedAdmin.Password))
        {
            var problem = DescribePasswordPolicyViolation(spec.SeedAdmin.Password);
            if (problem is not null)
            {
                errors.Add($"'seedAdmin.password' üretilen Identity servisinin parola politikasına uymuyor: {problem} " +
                           "(Program.cs'deki AddIdentity Password politikası ile aynı — aksi halde seed admin oluşturulurken servis çöker).");
            }
        }

        return errors;
    }

    /// <summary>
    /// services/BaseForge.Identity/Program.cs'deki AddIdentity Password politikasıyla birebir aynı kural seti
    /// (RequiredLength = 8; diğerleri ASP.NET Core Identity varsayılanı: RequireDigit/Uppercase/Lowercase/
    /// NonAlphanumeric = true). Politika orada değişirse burası da güncellenmelidir.
    /// </summary>
    private static string? DescribePasswordPolicyViolation(string password)
    {
        var missing = new List<string>();

        if (password.Length < 8)
        {
            missing.Add("en az 8 karakter");
        }

        if (!password.Any(char.IsUpper))
        {
            missing.Add("en az bir büyük harf");
        }

        if (!password.Any(char.IsLower))
        {
            missing.Add("en az bir küçük harf");
        }

        if (!password.Any(char.IsDigit))
        {
            missing.Add("en az bir rakam");
        }

        if (password.All(char.IsLetterOrDigit))
        {
            missing.Add("en az bir özel (alfanümerik olmayan) karakter");
        }

        return missing.Count > 0 ? string.Join(", ", missing) + " gerekli" : null;
    }
}
