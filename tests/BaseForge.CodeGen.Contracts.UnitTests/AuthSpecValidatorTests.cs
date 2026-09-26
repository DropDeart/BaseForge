namespace BaseForge.CodeGen.Contracts.UnitTests;

public class AuthSpecValidatorTests
{
    [Fact]
    public void Validate_AuthYaml_ReturnsNoErrors()
    {
        var spec = SpecLoader.Load<AuthSpec>(SampleSpecs.Auth);

        var errors = AuthSpecValidator.Validate(spec);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingServiceAndDatabase_ReturnsErrors()
    {
        var spec = new AuthSpec();

        var errors = AuthSpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("'service'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'database'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("short1A")] // 7 karakter — 8'den az
    [InlineData("alllowercase1")] // büyük harf yok
    [InlineData("ALLUPPERCASE1")] // küçük harf yok
    [InlineData("NoDigitsHere")] // rakam yok
    [InlineData("Alphanum3ric")] // özel karakter yok
    public void Validate_WeakSeedAdminPassword_ReturnsError(string weakPassword)
    {
        var spec = new AuthSpec
        {
            Service = "identity",
            Database = "identity_db",
            SeedAdmin = new SeedAdminSpec { Email = "admin@example.com", Password = weakPassword },
        };

        var errors = AuthSpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("'seedAdmin.password'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_StrongSeedAdminPassword_ReturnsNoErrors()
    {
        var spec = new AuthSpec
        {
            Service = "identity",
            Database = "identity_db",
            SeedAdmin = new SeedAdminSpec { Email = "admin@example.com", Password = "Admin!2345" },
        };

        var errors = AuthSpecValidator.Validate(spec);

        Assert.Empty(errors);
    }

    [Fact]
    public void Registration_DefaultsToClosedWithUserRole()
    {
        var spec = new AuthSpec();

        Assert.False(spec.Registration.Enabled);
        Assert.Equal("User", spec.Registration.DefaultRole);
    }

    [Fact]
    public void AllRoles_AlwaysIncludesAdminAndUserWithoutDuplicates()
    {
        var spec = new AuthSpec { Roles = ["Editor", "Admin", "SuperAdmin"] };

        Assert.Equal(["Admin", "User", "Editor", "SuperAdmin"], AuthSpecValidator.AllRoles(spec));
    }

    [Fact]
    public void Validate_UnknownDefaultRoleAndInvalidRoleName_ReturnErrors()
    {
        var spec = new AuthSpec
        {
            Service = "identity",
            Database = "identity_db",
            Roles = ["Site Admin"],
            Registration = new RegistrationSpec { Enabled = true, DefaultRole = "Member" },
        };

        var errors = AuthSpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("geçersiz rol adı: 'Site Admin'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'registration.defaultRole' ('Member')", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DefaultRoleFromCustomRoles_ReturnsNoErrors()
    {
        var spec = new AuthSpec
        {
            Service = "identity",
            Database = "identity_db",
            Roles = ["Doctor"],
            Registration = new RegistrationSpec { Enabled = true, DefaultRole = "Doctor" },
        };

        Assert.Empty(AuthSpecValidator.Validate(spec));
    }
}
