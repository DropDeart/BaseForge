namespace BaseForge.CodeGen.Spec;

/// <summary>Merkez auth (Identity) servisinin deklaratif tanımı (auth.yaml).</summary>
public sealed class AuthSpec
{
    public string Service { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public SigningSpec? Signing { get; set; }

    public List<AuthScopeSpec> Scopes { get; set; } = [];

    public List<AuthClientSpec> Clients { get; set; } = [];

    public SeedAdminSpec? SeedAdmin { get; set; }

    public ProvidersSpec Providers { get; set; } = new();
}

public sealed class SigningSpec
{
    public string? CertificatePath { get; set; }

    public string? CertificatePassword { get; set; }
}

public sealed class AuthScopeSpec
{
    public string Name { get; set; } = string.Empty;

    public string? Resource { get; set; }
}

public sealed class AuthClientSpec
{
    public string ClientId { get; set; } = string.Empty;

    public string? Secret { get; set; }

    public bool Public { get; set; }

    public List<string> Grants { get; set; } = [];

    public List<string> Scopes { get; set; } = [];

    public List<string> RedirectUris { get; set; } = [];
}

public sealed class SeedAdminSpec
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class ProvidersSpec
{
    public ProviderSpec? Google { get; set; }

    public ProviderSpec? GitHub { get; set; }

    public ProviderSpec? Microsoft { get; set; }

    public ProviderSpec? Facebook { get; set; }
}

public sealed class ProviderSpec
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
