namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>auth.yaml multiTenancy ve registration.selfAssignableRoles (0.6.4).</summary>
public class MultiTenancySpecTests
{
    private static AuthSpec Load(string yaml) => SampleSpecs.LoadYaml<AuthSpec>(yaml);

    private const string Valid = """
        service: identity
        database: identity_db
        roles: [SuperAdmin, TenantAdmin, Operator, Driver]
        registration:
          enabled: true
          selfAssignableRoles: [Driver]
        multiTenancy:
          enabled: true
          adminRole: TenantAdmin
          memberRoles: [Operator]
          requireApproval: true
          approverRoles: [SuperAdmin]
        """;

    [Fact]
    public void Parses_AndValidates()
    {
        var spec = Load(Valid);

        Assert.True(spec.MultiTenancy.Enabled);
        Assert.Equal("TenantAdmin", spec.MultiTenancy.AdminRole);
        Assert.Equal(["Operator"], spec.MultiTenancy.MemberRoles);
        Assert.Equal(["Driver"], spec.Registration.SelfAssignableRoles);
        Assert.Empty(AuthSpecValidator.Validate(spec));
    }

    [Fact]
    public void Disabled_ByDefault()
        => Assert.False(Load("service: identity\ndatabase: identity_db\n").MultiTenancy.Enabled);

    [Fact]
    public void UnknownRoles_AreRejected()
    {
        var spec = Load(Valid.Replace("memberRoles: [Operator]", "memberRoles: [Operator, Kantarci]", StringComparison.Ordinal)
                             .Replace("selfAssignableRoles: [Driver]", "selfAssignableRoles: [Pilot]", StringComparison.Ordinal));

        var errors = AuthSpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("'Kantarci'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'Pilot'", StringComparison.Ordinal));
    }

    [Fact]
    public void MemberRole_CannotBeGlobal()
    {
        var spec = Load(Valid.Replace("selfAssignableRoles: [Driver]", "selfAssignableRoles: [Operator]", StringComparison.Ordinal)
                             .Replace("approverRoles: [SuperAdmin]", "approverRoles: [TenantAdmin]", StringComparison.Ordinal));

        var errors = AuthSpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("'Operator' şirket içi rol", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'TenantAdmin' şirket içi rol", StringComparison.Ordinal));
    }
}
