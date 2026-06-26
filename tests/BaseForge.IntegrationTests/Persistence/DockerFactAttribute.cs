using Xunit;

namespace BaseForge.IntegrationTests.Persistence;

/// <summary>
/// Docker gerektiren testleri işaretler. <c>RUN_DB_TESTS=1</c> ortam değişkeni ayarlı
/// değilse test atlanır (yerelde Docker yoksa; CI'da etkindir).
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DB_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Docker gerektirir; RUN_DB_TESTS=1 ile etkinleşir (CI'da açık).";
        }
    }
}
