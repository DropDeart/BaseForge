namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>filterable (liste filtreleri) ve readFilter (okuma görünürlüğü).</summary>
public class ListOptionsSpecTests
{
    [Fact]
    public void Load_FilterableAndReadFilter_Parses()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """
                service: blog
                database: blog_db
                entities:
                  Post:
                    props:
                      IsPublished: bool
                      AuthorId: guid
                    ownerField: AuthorId
                    filterable: [AuthorId]
                    readFilter:
                      where: { IsPublished: true }
                      bypassRoles: [Admin]
                      bypassOwner: true
                    access:
                      create: authenticated
                auth:
                  authority: http://localhost:8081
                """);

            var spec = SpecLoader.Load(path);
            var post = spec.Entities["Post"];

            Assert.Equal(["AuthorId"], post.Filterable);
            Assert.Equal("true", post.ReadFilter!.Where["IsPublished"]);
            Assert.Equal(["Admin"], post.ReadFilter.BypassRoles);
            Assert.True(post.ReadFilter.BypassOwner);
            Assert.Empty(SpecValidator.Validate(spec));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ScalarFieldTypes_IncludesRelationAndExternalRefIds()
    {
        var entity = new EntitySpec
        {
            Props = { ["title"] = new PropSpec { Type = "string" } },
            Relations = { ["category"] = new RelationSpec { Kind = "many-to-one", Target = "Category" }, ["items"] = new RelationSpec { Kind = "one-to-many", Target = "Item" } },
            ExternalRefs = { ["seller"] = new ExternalRefSpec { Store = "SellerId" } },
        };

        var fields = SpecValidator.ScalarFieldTypes(entity);

        Assert.Equal(["Title", "CategoryId", "SellerId"], fields.Keys);
        Assert.Equal("guid", fields["CategoryId"]);
    }

    [Theory]
    [InlineData("Nope", "bu entity'nin bir alanı değil")]
    [InlineData("Body", "eşitlik filtresi için uygun değil")]
    [InlineData("Search", "ayrılmış bir sorgu parametresi")]
    public void Validate_InvalidFilterable_ReturnsError(string field, string expected)
    {
        var entity = new EntitySpec
        {
            Props = { ["Body"] = new PropSpec { Type = "text" }, ["Search"] = new PropSpec { Type = "string" } },
            Filterable = [field],
        };

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_FilterableOnUnpaginatedList_ReturnsError()
    {
        var entity = new EntitySpec { Props = { ["Title"] = new PropSpec { Type = "string" } }, Filterable = ["Title"], Paginated = false };

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains("yalnızca sayfalı", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("IsPublished", "evet", "geçersiz")]
    [InlineData("Status", "Gone", "geçersiz")]
    [InlineData("Price", "1", "desteklenmiyor")]
    [InlineData("Missing", "true", "bir prop'u değil")]
    public void Validate_InvalidReadFilterWhere_ReturnsError(string field, string value, string expected)
    {
        var entity = new EntitySpec
        {
            Props =
            {
                ["IsPublished"] = new PropSpec { Type = "bool" },
                ["Status"] = new PropSpec { Type = "enum", Values = ["Draft", "Live"] },
                ["Price"] = new PropSpec { Type = "decimal" },
            },
            ReadFilter = new ReadFilterSpec { Where = { [field] = value } },
        };

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BypassOwnerWithoutOwnerField_ReturnsError()
    {
        var entity = new EntitySpec
        {
            Props = { ["IsPublished"] = new PropSpec { Type = "bool" } },
            ReadFilter = new ReadFilterSpec { Where = { ["IsPublished"] = "true" }, BypassOwner = true },
        };

        Assert.Contains(SpecValidator.Validate(Spec(entity)), e => e.Contains("'ownerField' tanımlanmalı", StringComparison.Ordinal));
    }

    private static ServiceSpec Spec(EntitySpec entity) => new() { Service = "s", Database = "s_db", Entities = { ["Post"] = entity } };
}
