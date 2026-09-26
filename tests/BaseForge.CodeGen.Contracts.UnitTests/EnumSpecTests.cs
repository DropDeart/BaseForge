namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>enum prop tipi.</summary>
public class EnumSpecTests
{
    [Fact]
    public void Load_EnumProp_ParsesValuesAndDefault()
    {
        var spec = Load(
            """
            service: market
            database: market_db
            entities:
              Listing:
                props:
                  Status:
                    type: enum
                    values: [Draft, Active, Sold]
                    default: Draft
            """);

        var status = spec.Entities["Listing"].Props["Status"];
        Assert.Equal(["Draft", "Active", "Sold"], status.Values);
        Assert.Equal("Draft", status.Default);
        Assert.Empty(SpecValidator.Validate(spec));
        Assert.Equal("ListingStatus", TypeMap.EnumTypeName("Listing", "status"));
    }

    [Theory]
    [InlineData(new string[0], "en az bir değer")]
    [InlineData(new[] { "In Review" }, "geçerli bir C# tanımlayıcısı değil")]
    [InlineData(new[] { "Active", "active" }, "tekrar eden değer")]
    public void Validate_InvalidEnumValues_ReturnsError(string[] values, string expected)
    {
        var spec = Spec(new PropSpec { Type = "enum", Values = [.. values] });

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DefaultNotInValues_ReturnsError()
    {
        var spec = Spec(new PropSpec { Type = "enum", Values = ["Draft"], Default = "Active" });

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("'default' ('Active') 'values' içinde yok", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ValuesOnNonEnum_ReturnsError()
    {
        var spec = Spec(new PropSpec { Type = "string", Values = ["A"] });

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("'values' yalnızca 'enum' tipinde", StringComparison.Ordinal));
    }

    private static ServiceSpec Spec(PropSpec prop) => new()
    {
        Service = "s",
        Database = "s_db",
        Entities = { ["Listing"] = new EntitySpec { Props = { ["Status"] = prop } } },
    };

    private static ServiceSpec Load(string yaml)
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, yaml);
            return SpecLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
