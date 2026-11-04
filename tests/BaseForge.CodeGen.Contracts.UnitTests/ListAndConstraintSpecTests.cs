namespace BaseForge.CodeGen.Contracts.UnitTests;

/// <summary>0.6.4: rangeFilters, inFilters, defaultSort, unique, min/max/minLength/pattern, nullable externalRef.</summary>
public class ListAndConstraintSpecTests
{
    private const string Valid = """
        service: shop
        database: shop_db
        entities:
          Order:
            rangeFilters: [PlacedAt, Total]
            inFilters: [Status]
            defaultSort: placedAt desc, orderNo
            unique: [[OrderNo]]
            props:
              orderNo: { type: string, maxLength: 20, minLength: 3, pattern: '^[A-Z0-9-]+$' }
              status: { type: enum, values: [Open, Paid] }
              total: { type: decimal, min: 0, max: 1000000 }
              placedAt: datetime
            externalRefs:
              customer: { target: crm/Customer, store: CustomerId, via: grpc, nullable: true }
        """;

    [Fact]
    public void Parses_AndValidates()
    {
        var spec = SampleSpecs.LoadYaml<ServiceSpec>(Valid);
        var order = spec.Entities["Order"];

        Assert.Equal(["PlacedAt", "Total"], order.RangeFilters);
        Assert.Equal(["Status"], order.InFilters);
        Assert.Equal("placedAt desc, orderNo", order.DefaultSort);
        Assert.Equal(["OrderNo"], Assert.Single(order.Unique));
        Assert.Equal(0m, order.Props["total"].Min);
        Assert.Equal(3, order.Props["orderNo"].MinLength);
        Assert.Equal("^[A-Z0-9-]+$", order.Props["orderNo"].Pattern);
        Assert.True(order.ExternalRefs["customer"].Nullable);
        Assert.Empty(SpecValidator.Validate(spec));
    }

    [Fact]
    public void InvalidUses_AreRejected()
    {
        var spec = SampleSpecs.LoadYaml<ServiceSpec>(Valid
            .Replace("rangeFilters: [PlacedAt, Total]", "rangeFilters: [OrderNo, Missing]", StringComparison.Ordinal)
            .Replace("defaultSort: placedAt desc, orderNo", "defaultSort: placedAt sideways", StringComparison.Ordinal)
            .Replace("unique: [[OrderNo]]", "unique: [[Nope]]", StringComparison.Ordinal)
            .Replace("min: 0, max: 1000000", "min: 10, max: 1", StringComparison.Ordinal)
            .Replace("placedAt: datetime", "placedAt: { type: datetime, pattern: 'x' }", StringComparison.Ordinal));

        var errors = SpecValidator.Validate(spec);
        Assert.Contains(errors, e => e.Contains("rangeFilters' — 'OrderNo' (string)", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("rangeFilters' — 'Missing'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("defaultSort' geçersiz", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("unique' — 'Nope'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("min, max'tan büyük", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("minLength/pattern yalnızca", StringComparison.Ordinal));
    }

    [Fact]
    public void BadPattern_IsRejected()
    {
        var spec = SampleSpecs.LoadYaml<ServiceSpec>(Valid.Replace("pattern: '^[A-Z0-9-]+$'", "pattern: '[unclosed'", StringComparison.Ordinal));
        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("pattern' geçerli bir düzenli ifade değil", StringComparison.Ordinal));
    }

    [Fact]
    public void CrossTenantRoles_RequireMultiTenant()
    {
        var spec = SampleSpecs.LoadYaml<ServiceSpec>(Valid.Replace("database: shop_db", "database: shop_db\nauth:\n  authority: http://x\n  crossTenantRoles: [SuperAdmin]", StringComparison.Ordinal));
        Assert.Contains(SpecValidator.Validate(spec), e => e.Contains("crossTenantRoles' yalnızca 'multiTenant: true'", StringComparison.Ordinal));
    }
}
