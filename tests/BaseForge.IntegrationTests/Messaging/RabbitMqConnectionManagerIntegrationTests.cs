using BaseForge.Infrastructure.Messaging;
using BaseForge.IntegrationTests.Persistence;

namespace BaseForge.IntegrationTests.Messaging;

/// <summary>
/// <see cref="RabbitMqConnectionManager"/>'ın kanal havuzunu GERÇEK bir RabbitMQ broker'ına
/// karşı doğrular (Testcontainers): kiralanıp iade edilen bir kanalın tekrar kullanıldığı,
/// havuz kapasitesi aşıldığında yeni kanal açmanın (bloklamadan) devam ettiği.
/// </summary>
public sealed class RabbitMqConnectionManagerIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _fixture;

    public RabbitMqConnectionManagerIntegrationTests(RabbitMqFixture fixture) => _fixture = fixture;

    [DockerFact]
    public async Task RentThenReturn_ThenRentAgain_ReusesSameChannel()
    {
        await using var manager = new RabbitMqConnectionManager(BuildOptions());

        var channel = await manager.RentChannelAsync();
        await manager.ReturnChannelAsync(channel);
        var reused = await manager.RentChannelAsync();

        Assert.Same(channel, reused);
        await manager.ReturnChannelAsync(reused);
    }

    [DockerFact]
    public async Task RentingBeyondPoolCapacity_StillSucceeds_WithoutBlocking()
    {
        await using var manager = new RabbitMqConnectionManager(BuildOptions());

        // Havuz kapasitesinin (10) üzerinde kanal aynı anda kiralanır ve HİÇBİRİ iade edilmeden
        // yeni bir kiralama daha yapılır — havuz "best effort" bir önbellek olduğu için bu asla
        // bloklamamalı (sınırlı boyutlu bir Channel<T> Writer.TryWrite ile doldurulur, Rent ise
        // her zaman havuz boşsa doğrudan yeni bir kanal açar).
        var rented = new List<RabbitMQ.Client.IChannel>();
        for (var i = 0; i < 15; i++)
        {
            rented.Add(await manager.RentChannelAsync());
        }

        Assert.Equal(15, rented.Count);
        Assert.All(rented, c => Assert.True(c.IsOpen));

        foreach (var channel in rented)
        {
            await manager.ReturnChannelAsync(channel);
        }
    }

    private RabbitMqOptions BuildOptions() => new()
    {
        Host = _fixture.Host,
        Port = _fixture.Port,
        Username = _fixture.Username,
        Password = _fixture.Password,
    };
}
