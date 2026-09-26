namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>Opsiyonel (nullable) servis içi ilişkiler.</summary>
public class RelationSpecTests
{
    [Fact]
    public void Load_NullableRelation_ParsesFlag()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                """
                service: catalog
                database: catalog_db
                entities:
                  Category:
                    props:
                      Name: string
                    relations:
                      parent:
                        kind: many-to-one
                        target: Category
                        nullable: true
                """);

            var spec = SpecLoader.Load(path);

            Assert.True(spec.Entities["Category"].Relations["parent"].Nullable);
            Assert.Empty(SpecValidator.Validate(spec));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Validate_NullableOneToMany_ReturnsError()
    {
        var spec = new ServiceSpec
        {
            Service = "s",
            Database = "s_db",
            Entities =
            {
                ["Order"] = new EntitySpec
                {
                    Props = { ["Total"] = new PropSpec { Type = "decimal" } },
                    Relations = { ["items"] = new RelationSpec { Kind = "one-to-many", Target = "Item", Nullable = true } },
                },
                ["Item"] = new EntitySpec { Props = { ["Qty"] = new PropSpec { Type = "int" } } },
            },
        };

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("'nullable' yalnızca many-to-one/one-to-one", StringComparison.Ordinal));
    }
}
