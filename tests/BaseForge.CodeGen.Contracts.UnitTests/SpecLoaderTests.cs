namespace BaseForge.CodeGen.Contracts.UnitTests;

public class SpecLoaderTests
{
    [Fact]
    public void Load_BlogYaml_ParsesExpectedEntities()
    {
        var spec = SpecLoader.Load(SampleSpecs.Blog);

        Assert.Equal("blog", spec.Service);
        Assert.Equal("blog_db", spec.Database);
        Assert.Equal(["Post", "Comment", "Like"], spec.Entities.Keys.ToArray());

        var comment = spec.Entities["Comment"];
        Assert.Equal("text", comment.Props["body"].Type);
        Assert.Equal("many-to-one", comment.Relations["post"].Kind);
        Assert.Equal("Post", comment.Relations["post"].Target);
        Assert.Equal("AuthorId", comment.ExternalRefs["author"].Store);
        Assert.Equal(["created"], comment.Publishes);

        Assert.NotNull(spec.Subscribes);
        Assert.Equal(2, spec.Subscribes!.Count);
        Assert.Equal("blog/CommentCreated", spec.Subscribes[0].Event);
        Assert.Equal("NotifyPostAuthorOnComment", spec.Subscribes[0].Handler);
    }

    [Fact]
    public void Load_OrdersYaml_ParsesRelationsAndExternalRefs()
    {
        var spec = SpecLoader.Load(SampleSpecs.Orders);

        Assert.Equal("orders", spec.Service);
        var order = spec.Entities["Order"];
        Assert.Equal("decimal", order.Props["total"].Type);
        Assert.Equal("one-to-many", order.Relations["items"].Kind);
        Assert.Equal("OrderItem", order.Relations["items"].Target);
        Assert.Equal("customers/Customer", order.ExternalRefs["customer"].Target);
        Assert.Equal("CustomerId", order.ExternalRefs["customer"].Store);

        // auth bloğu örnekte yorum satırında — spec'te tanımsız kalmalı.
        Assert.Null(spec.Auth);
    }

    [Fact]
    public void Load_AuthYaml_ParsesClientsAndSeedAdmin()
    {
        var spec = SpecLoader.Load<AuthSpec>(SampleSpecs.Auth);

        Assert.Equal("identity", spec.Service);
        Assert.Equal(3, spec.Clients.Count);
        Assert.Equal("spa-client", spec.Clients[0].ClientId);
        Assert.True(spec.Clients[0].Public);
        Assert.Equal(["password", "refresh_token"], spec.Clients[0].Grants);

        Assert.NotNull(spec.SeedAdmin);
        Assert.Equal("admin@baseforge.local", spec.SeedAdmin!.Email);
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var missingPath = SampleSpecs.Path("does-not-exist.yaml");

        Assert.Throws<FileNotFoundException>(() => SpecLoader.Load(missingPath));
    }

    [Theory]
    [InlineData("plain: string")]
    [InlineData("rich: { type: string, nullable: true, maxLength: 120, default: \"hello\" }")]
    public void PropSpecYamlConverter_RoundTrips_ScalarAndRichForms(string yamlFragment)
    {
        var yaml = $"""
            service: roundtrip
            database: roundtrip_db
            entities:
              Sample:
                props:
                  {yamlFragment}
            """;

        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new PropSpecYamlConverter())
            .IgnoreUnmatchedProperties()
            .Build();
        var spec = deserializer.Deserialize<ServiceSpec>(yaml);

        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new PropSpecYamlConverter())
            .Build();
        var written = serializer.Serialize(spec);
        var reloaded = deserializer.Deserialize<ServiceSpec>(written);

        var original = spec.Entities["Sample"].Props.Single().Value;
        var roundTripped = reloaded.Entities["Sample"].Props.Single().Value;
        Assert.Equal(original.Type, roundTripped.Type);
        Assert.Equal(original.Nullable, roundTripped.Nullable);
        Assert.Equal(original.MaxLength, roundTripped.MaxLength);
        Assert.Equal(original.Default, roundTripped.Default);
    }
}
