namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>auth.yaml otp bölümü (0.6.4).</summary>
public class OtpSpecTests
{
    private static AuthSpec Load(string yaml) => SampleSpecs.LoadYaml<AuthSpec>(yaml);

    private const string Base = """
        service: identity
        database: identity_db
        clients:
          - clientId: app
            public: true
            grants: [password, otp, refresh_token]
            scopes: [api]

        """;

    [Fact]
    public void Parses_AndValidates()
    {
        var spec = Load(Base + "otp:\n  enabled: true\n  codeLength: 6\n  defaultCountryCode: \"+90\"\n");

        Assert.True(spec.Otp.Enabled);
        Assert.Equal(180, spec.Otp.LifetimeSeconds);
        Assert.Empty(AuthSpecValidator.Validate(spec));
    }

    [Fact]
    public void OtpGrant_RequiresOtpEnabled()
        => Assert.Contains(AuthSpecValidator.Validate(Load(Base)), e => e.Contains("'otp' grant", StringComparison.Ordinal));

    [Fact]
    public void InvalidValues_AreRejected()
    {
        var spec = Load(Base + "otp:\n  enabled: true\n  codeLength: 12\n  defaultCountryCode: \"90\"\n  smsProvider: Twilio\n");

        var errors = AuthSpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("codeLength", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("defaultCountryCode", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'Twilio'", StringComparison.Ordinal));
    }
}
