using Testcontainers.RabbitMq;
using Xunit;

namespace BaseForge.IntegrationTests.Messaging;

/// <summary>
/// Test süresince tek bir gerçek RabbitMQ container'ı (Testcontainers) sağlar.
/// <c>RUN_DB_TESTS=1</c> ayarlı değilse container başlatılmaz (testler zaten atlanır) — bkz.
/// <see cref="Persistence.DockerFactAttribute"/>, <see cref="Persistence.PostgresFixture"/> ile aynı desen.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string Host { get; private set; } = string.Empty;

    public int Port { get; private set; }

    public string Username { get; private set; } = "guest";

    public string Password { get; private set; } = "guest";

    public async Task InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_DB_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        _container = new RabbitMqBuilder("rabbitmq:4-management-alpine")
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();
        await _container.StartAsync();
        Host = _container.Hostname;
        Port = _container.GetMappedPublicPort(5672);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
