namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>0.6.3 güvenlik seçenekleri: serverManaged, sortFields/searchFields, access.counters.</summary>
public class SecuritySpecTests
{
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

    [Fact]
    public void ServerManaged_ParsesAndValidates()
    {
        var spec = Load(
            """
            service: shop
            database: shop_db
            entities:
              Order:
                props:
                  status: { type: enum, values: [Draft, Approved], serverManaged: true }
                  total: { type: decimal, serverManaged: true }
                  note: string
            """);

        var props = spec.Entities["Order"].Props;
        Assert.True(props["status"].ServerManaged);
        Assert.True(props["total"].ServerManaged);
        Assert.False(props["note"].ServerManaged);
        Assert.Empty(SpecValidator.Validate(spec));
    }

    [Fact]
    public void ServerManaged_OnOwnerField_IsRejected()
    {
        var spec = Load(
            """
            service: shop
            database: shop_db
            auth:
              authority: http://localhost:8081
            entities:
              Order:
                ownerField: OwnerId
                props:
                  ownerId: { type: guid, serverManaged: true }
            """);

        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("serverManaged olamaz", StringComparison.Ordinal));
    }

    [Fact]
    public void SortAndSearchFields_UnknownOrWrongType_AreRejected()
    {
        var spec = Load(
            """
            service: shop
            database: shop_db
            entities:
              Order:
                sortFields: [OrderNo, CreatedAt, Missing]
                searchFields: [OrderNo, Total]
                props:
                  orderNo: string
                  total: decimal
            """);

        var errors = SpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("sortFields' — 'Missing'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("searchFields' — 'Total'", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.Contains("'CreatedAt'", StringComparison.Ordinal));
    }

    [Fact]
    public void CountersAccess_RequiresCountersAndRejectsOwner()
    {
        var withoutCounters = Load(
            """
            service: blog
            database: blog_db
            auth:
              authority: http://localhost:8081
            entities:
              Post:
                access:
                  counters: authenticated
                props:
                  title: string
            """);
        Assert.Contains(SpecValidator.Validate(withoutCounters), e => e.Contains("sayacı (counters) yok", StringComparison.Ordinal));

        var valid = Load(
            """
            service: blog
            database: blog_db
            auth:
              authority: http://localhost:8081
            entities:
              Post:
                counters: [Views]
                access:
                  counters: authenticated
                props:
                  title: string
                  views: int
            """);
        Assert.Empty(SpecValidator.Validate(valid));
    }
}
