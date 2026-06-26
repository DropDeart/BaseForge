using Testcontainers.PostgreSql;
using Xunit;

namespace BaseForge.IntegrationTests.Persistence;

/// <summary>
/// Test süresince tek bir gerçek PostgreSQL container'ı (Testcontainers) sağlar.
/// <c>RUN_DB_TESTS=1</c> ayarlı değilse container başlatılmaz (testler zaten atlanır).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DB_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
