namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>auth.yaml messaging, passwordReset ve accessTokenLifetimeMinutes (0.6.4).</summary>
public class MessagingSpecTests
{
    private const string Base = "service: identity\ndatabase: identity_db\n";

    [Fact]
    public void Smtp_AndPasswordReset_Parse()
    {
        var spec = SampleSpecs.LoadYaml<AuthSpec>(Base + """
            messaging:
              emailProvider: Smtp
              smtp:
                host: smtp.resend.com
                port: 465
                username: resend
                fromAddress: no-reply@ornek.com
                fromName: Örnek
            passwordReset:
              enabled: true
              resetUrl: https://app.ornek.com/sifre-sifirla
            accessTokenLifetimeMinutes: 30
            """);

        Assert.Equal("Smtp", spec.Messaging.EmailProvider);
        Assert.Equal(465, spec.Messaging.Smtp.Port);
        Assert.True(spec.PasswordReset.Enabled);
        Assert.Equal(2, spec.PasswordReset.TokenLifetimeHours);
        Assert.Equal(30, spec.AccessTokenLifetimeMinutes);
        Assert.Empty(AuthSpecValidator.Validate(spec));
    }

    [Fact]
    public void InvalidValues_AreRejected()
    {
        var spec = SampleSpecs.LoadYaml<AuthSpec>(Base + """
            messaging:
              emailProvider: Smtp
            passwordReset:
              enabled: true
              resetUrl: /sifre-sifirla
            accessTokenLifetimeMinutes: 2
            """);

        var errors = AuthSpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("'messaging.smtp'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("resetUrl", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("accessTokenLifetimeMinutes", StringComparison.Ordinal));
    }
}
