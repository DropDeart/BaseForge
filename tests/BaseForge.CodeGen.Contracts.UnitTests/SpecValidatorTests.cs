namespace BaseForge.CodeGen.Contracts.UnitTests;

public class SpecValidatorTests
{
    [Fact]
    public void Validate_BlogYaml_ReturnsNoErrors()
    {
        var spec = SpecLoader.Load(SampleSpecs.Blog);

        var errors = SpecValidator.Validate(spec);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_OrdersYaml_ReturnsNoErrors()
    {
        var spec = SpecLoader.Load(SampleSpecs.Orders);

        var errors = SpecValidator.Validate(spec);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingServiceAndDatabase_ReturnsErrors()
    {
        var spec = new ServiceSpec
        {
            Entities = { ["Foo"] = new EntitySpec { Props = { ["bar"] = new PropSpec { Type = "string" } } } },
        };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("'service'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("'database'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NoEntities_ReturnsError()
    {
        var spec = new ServiceSpec { Service = "empty", Database = "empty_db" };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("entities", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UnknownPropType_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities = { ["Widget"] = new EntitySpec { Props = { ["color"] = new PropSpec { Type = "rgba" } } } },
        };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("bilinmeyen tip", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RelationTargetNotInSameService_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities =
            {
                ["Order"] = new EntitySpec
                {
                    Relations = { ["items"] = new RelationSpec { Kind = "one-to-many", Target = "OrderItem" } },
                },
            },
        };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("aynı serviste tanımlı değil", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AppendOnlyWithUpdatePublish_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities =
            {
                ["Log"] = new EntitySpec
                {
                    AppendOnly = true,
                    Publishes = ["created", "updated"],
                },
            },
        };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("appendOnly=true", StringComparison.Ordinal) && e.Contains("publishes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MaxLengthOnNonStringType_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities =
            {
                ["Widget"] = new EntitySpec { Props = { ["count"] = new PropSpec { Type = "int", MaxLength = 10 } } },
            },
        };

        var errors = SpecValidator.Validate(spec);

        Assert.Contains(errors, e => e.Contains("maxLength", StringComparison.Ordinal));
    }
}
