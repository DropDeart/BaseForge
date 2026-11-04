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

        foreach (var role in spec.Roles)
        {
            if (string.IsNullOrWhiteSpace(role) || !role.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
            {
                errors.Add($"'roles' geçersiz rol adı: '{role}' (harf, rakam, '_' veya '-').");
            }
        }

        var defaultRole = spec.Registration.DefaultRole;
        if (!AllRoles(spec).Contains(defaultRole, StringComparer.Ordinal))
        {
            errors.Add($"'registration.defaultRole' ('{defaultRole}') tanımlı bir rol değil (tanımlı: {string.Join(", ", AllRoles(spec))}).");
        }

        foreach (var role in spec.Registration.SelfAssignableRoles.Where(r => !AllRoles(spec).Contains(r, StringComparer.Ordinal)))
        {
            errors.Add($"'registration.selfAssignableRoles' tanımlı bir rol değil: '{role}'.");
        }

        if (spec.MultiTenancy.Enabled)
        {
            ValidateMultiTenancy(spec, errors);
        }

        ValidateOtp(spec, errors);

        if (spec.UserProfile is not null)
        {
            ValidateUserProfile(spec.UserProfile, errors);
        }

        return errors;
    }

    /// <summary><c>ApplicationUser</c>'ın (IdentityUser + BaseForge) mevcut üyeleri — profil alanı bu adları alamaz.</summary>
    private static readonly HashSet<string> ReservedProfileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "PasswordHash",
        "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
        "LockoutEnd", "LockoutEnabled", "AccessFailedCount", "FullName", "AvatarUrl", "Roles", "HasPassword", "Profile",
    };

    /// <summary>Token'da zaten bulunan / OIDC-JWT'de ayrılmış claim adları — <c>inToken</c> alanı bunlarla çakışamaz.</summary>
    private static readonly HashSet<string> ReservedClaimNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "sub", "name", "email", "role", "scope", "aud", "iss", "exp", "iat", "nbf", "jti", "azp", "client_id",
        "oi_prst", "oi_au_id", "oi_tkn_id", "oi_scp", "oi_rsrc", "at_hash", "nonce", "auth_time",
    };

    private static void ValidateUserProfile(UserProfileSpec profile, List<string> errors)
    {
        foreach (var (propName, prop) in profile.Props)
        {
            var label = $"userProfile.{propName}";
            SpecValidator.ValidateProp(label, propName, prop, errors);

            if (ReservedProfileNames.Contains(propName))
            {
                errors.Add($"'{label}' — '{propName}' kullanıcının mevcut bir alanı; başka bir ad seçin.");
            }

            if (string.Equals(prop.Type.Trim(), "json", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"'{label}' — 'json' tipi profil alanlarında desteklenmiyor.");
            }

            if (prop.EditableBy is not null
                && !string.Equals(prop.EditableBy, UserProfileSpec.EditableBySelf, StringComparison.OrdinalIgnoreCase)
                && !UserProfileSpec.IsAdminOnly(prop))
            {
                errors.Add($"'{label}.editableBy' geçersiz: '{prop.EditableBy}' (izinli: self, admin).");
            }

            if (prop.InToken && ReservedClaimNames.Contains(NameUtil.Camel(propName)))
            {
                errors.Add($"'{label}' — claim adı '{NameUtil.Camel(propName)}' standart bir claim ile çakışıyor; 'inToken' için başka bir ad seçin.");
            }
        }

        var duplicates = profile.Props.Keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var name in duplicates)
        {
            errors.Add($"'userProfile' — '{name}' alanı büyük/küçük harf farkıyla birden fazla kez tanımlı.");
        }
    }

    private static void ValidateOtp(AuthSpec spec, List<string> errors)
    {
        var otp = spec.Otp;
        var usesGrant = spec.Clients.Any(c => c.Grants.Contains("otp", StringComparer.OrdinalIgnoreCase));
        if (usesGrant && !otp.Enabled)
        {
            errors.Add("Bir client 'otp' grant'ı kullanıyor ama 'otp.enabled' kapalı.");
        }

        if (!otp.Enabled)
        {
            return;
        }

        if (otp.CodeLength is < 4 or > 8)
        {
            errors.Add("'otp.codeLength' 4-8 arasında olmalı.");
        }

        if (otp.LifetimeSeconds < 30 || otp.MaxAttempts < 1 || otp.MaxPerHour < 1 || otp.ResendCooldownSeconds < 0)
        {
            errors.Add("'otp' süre/sınır değerleri pozitif olmalı (lifetimeSeconds ≥ 30).");
        }

        if (!otp.DefaultCountryCode.StartsWith('+') || otp.DefaultCountryCode.Length < 2)
        {
            errors.Add("'otp.defaultCountryCode' '+' ile başlamalı (örn. +90).");
        }

        foreach (var (name, provider) in new[] { ("smsProvider", otp.SmsProvider), ("emailProvider", otp.EmailProvider) })
        {
            if (!OtpSpec.Providers.Contains(provider, StringComparer.Ordinal))
            {
                errors.Add($"'otp.{name}' '{provider}' desteklenmiyor (şimdilik: {string.Join(", ", OtpSpec.Providers)}).");
            }
        }
    }

    private static void ValidateMultiTenancy(AuthSpec spec, List<string> errors)
    {
        var tenancy = spec.MultiTenancy;
        var roles = AllRoles(spec);
        var memberRoles = tenancy.MemberRoles.Append(tenancy.AdminRole).ToList();

        foreach (var role in memberRoles.Concat(tenancy.ApproverRoles).Where(r => !roles.Contains(r, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            errors.Add($"'multiTenancy' rolü 'roles' listesinde yok: '{role}'.");
        }

        foreach (var role in tenancy.ApproverRoles.Concat(spec.Registration.SelfAssignableRoles).Append(spec.Registration.DefaultRole)
                     .Where(r => memberRoles.Contains(r, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
        {
            errors.Add($"'{role}' şirket içi rol; global rol (approverRoles / registration) olarak kullanılamaz.");
        }

        if (tenancy.ApproverRoles.Count == 0 && tenancy.RequireApproval)
        {
            errors.Add("'multiTenancy.requireApproval' açıkken en az bir 'approverRoles' rolü gerekir.");
        }
    }

    /// <summary>Identity'nin seed edeceği tüm roller: her zaman var olan Admin/User + <see cref="AuthSpec.Roles"/>.</summary>
    public static IReadOnlyList<string> AllRoles(AuthSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new[] { "Admin", RegistrationSpec.DefaultUserRole }.Concat(spec.Roles).Distinct(StringComparer.Ordinal).ToList();
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
