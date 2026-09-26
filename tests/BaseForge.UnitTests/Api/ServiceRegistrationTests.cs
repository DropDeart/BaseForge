using BaseForge.API.Extensions;
using BaseForge.API.Grpc;
using Microsoft.Extensions.DependencyInjection;

namespace BaseForge.UnitTests.Api;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddBaseForge_RegistersGrpcClientInterceptor()
    {
        // Üretilen Program.cs AddGrpcClient<...>().AddInterceptor<CorrelationIdClientInterceptor>() çağırır;
        // interceptor DI'da yoksa ilk gRPC çağrısında runtime'da patlıyordu.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBaseForge(_ => { });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<CorrelationIdClientInterceptor>());
    }
}
