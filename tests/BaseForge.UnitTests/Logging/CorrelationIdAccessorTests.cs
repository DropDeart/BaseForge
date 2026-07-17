using BaseForge.Infrastructure.Logging;

namespace BaseForge.UnitTests.Logging;

/// <summary>
/// <see cref="CorrelationIdAccessor"/>'ın <see cref="AsyncLocal{T}"/> tabanlı izolasyonunu doğrular:
/// aynı anda çalışan farklı akışlar birbirinin correlation id'sini görmemeli.
/// </summary>
public sealed class CorrelationIdAccessorTests
{
    [Fact]
    public void Current_DefaultsToNull()
    {
        var accessor = new CorrelationIdAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Current_RoundTrips_WithinSameFlow()
    {
        var accessor = new CorrelationIdAccessor();

        accessor.Current = "abc-123";

        Assert.Equal("abc-123", accessor.Current);
    }

    [Fact]
    public async Task Current_DoesNotLeak_AcrossConcurrentAsyncFlows()
    {
        var accessor = new CorrelationIdAccessor();

        async Task<string?> RunWithId(string id)
        {
            accessor.Current = id;
            await Task.Delay(10);
            return accessor.Current;
        }

        var results = await Task.WhenAll(RunWithId("flow-a"), RunWithId("flow-b"), RunWithId("flow-c"));

        Assert.Equal("flow-a", results[0]);
        Assert.Equal("flow-b", results[1]);
        Assert.Equal("flow-c", results[2]);
    }
}
