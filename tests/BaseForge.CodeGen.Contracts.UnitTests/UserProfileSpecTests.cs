using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>auth.yaml <c>userProfile</c> (docs/ARCH.md §6.3).</summary>
public class UserProfileSpecTests
{
    [Fact]
    public void Load_UserProfile_ParsesEditableByAndInToken()
    {
        var spec = Load(
            """
            service: identity
            database: identity_db
            userProfile:
              props:
                Specialty: string
                DiplomaNo: { type: string, nullable: true, maxLength: 32 }
                VerificationStatus: { type: enum, values: [Pending, Approved, Rejected], default: Pending, editableBy: admin, inToken: true }
            """);

        var props = spec.UserProfile!.Props;
        Assert.Equal(["Specialty", "DiplomaNo", "VerificationStatus"], props.Keys);
        Assert.False(UserProfileSpec.IsAdminOnly(props["Specialty"]));
        Assert.False(props["Specialty"].InToken);
        Assert.True(UserProfileSpec.IsAdminOnly(props["VerificationStatus"]));
        Assert.True(props["VerificationStatus"].InToken);
        Assert.Equal(32, props["DiplomaNo"].MaxLength);
        Assert.Empty(AuthSpecValidator.Validate(spec));
    }

    [Fact]
    public void Serialize_ProfileProp_RoundTripsEditableByAndInToken()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new PropSpecYamlConverter())
            .Build();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new PropSpecYamlConverter())
            .Build();

        var yaml = serializer.Serialize(new PropSpec { Type = "bool", EditableBy = "admin", InToken = true });
        var back = deserializer.Deserialize<PropSpec>(yaml);

        Assert.Equal("admin", back.EditableBy);
        Assert.True(back.InToken);
        // Varsayılanlardaysa düz scalar kalır (mevcut auth.yaml'lar değişmez).
        Assert.Equal("string", serializer.Serialize(new PropSpec { Type = "string" }).Trim());
    }

    [Theory]
    [InlineData("Email", "string", null, false, "mevcut bir alanı")]
    [InlineData("fullName", "string", null, false, "mevcut bir alanı")]
    [InlineData("Meta", "json", null, false, "'json' tipi profil alanlarında desteklenmiyor")]
    [InlineData("Level", "int", "owner", false, "'userProfile.Level.editableBy' geçersiz")]
    [InlineData("Role", "string", null, true, "standart bir claim ile çakışıyor")]
    [InlineData("Weird Name", "string", null, false, "geçersiz alan adı")]
    [InlineData("Age", "number", null, false, "bilinmeyen tip")]
    public void Validate_InvalidProfileProp_ReturnsError(string name, string type, string? editableBy, bool inToken, string expected)
    {
        var spec = Spec(name, new PropSpec { Type = type, EditableBy = editableBy, InToken = inToken });

        Assert.Contains(AuthSpecValidator.Validate(spec), e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ProfileEnumDefaultNotInValues_ReturnsSharedPropError()
    {
        var spec = Spec("Status", new PropSpec { Type = "enum", Values = ["Pending"], Default = "Done" });

        Assert.Contains(AuthSpecValidator.Validate(spec), e => e.Contains("'userProfile.Status' — 'default' ('Done') 'values' içinde yok", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CaseInsensitiveDuplicate_ReturnsError()
    {
        var spec = Spec("Title", new PropSpec { Type = "string" });
        spec.UserProfile!.Props["title"] = new PropSpec { Type = "string" };

        Assert.Contains(AuthSpecValidator.Validate(spec), e => e.Contains("birden fazla kez", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_EditableByInServiceSpec_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities = { ["Post"] = new EntitySpec { Props = { ["Title"] = new PropSpec { Type = "string", EditableBy = "admin" } } } },
        };

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("yalnızca auth.yaml 'userProfile'", StringComparison.Ordinal));
    }

    private static AuthSpec Spec(string name, PropSpec prop) => new()
    {
        Service = "identity",
        Database = "identity_db",
        UserProfile = new UserProfileSpec { Props = { [name] = prop } },
    };

    private static AuthSpec Load(string yaml)
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            return SpecLoader.Load<AuthSpec>(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
