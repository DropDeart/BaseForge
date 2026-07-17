using BaseForge.Core.Logging;
using BaseForge.Infrastructure.Logging;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BaseForge.API.Grpc;

/// <summary>
/// Gelen gRPC çağrılarının metadata'sındaki <c>correlation-id</c>'yi (bkz.
/// <see cref="CorrelationIdClientInterceptor"/>) okuyup <c>ICorrelationIdAccessor.EnterScope</c>
/// (<c>BaseForge.Infrastructure</c>) ile hem accessor'a hem Serilog log context'ine yazar. gRPC bir
/// HTTP isteği zincirinden değil doğrudan da çağrılabildiği için metadata'da id yoksa yeni bir tane üretir.
/// Üretilen servisin <c>AddGrpc(o => o.Interceptors.Add&lt;CorrelationIdServerInterceptor&gt;())</c>
/// çağrısıyla (BaseForge.CodeGen Program.cs şablonu) tüm gRPC servislerine otomatik eklenir.
/// </summary>
public sealed class CorrelationIdServerInterceptor : Interceptor
{
    private const string MetadataKey = "correlation-id";

    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    /// <summary>Yeni bir interceptor örneği oluşturur.</summary>
    /// <param name="correlationIdAccessor">Çağrının correlation id'sinin yazılacağı accessor.</param>
    public CorrelationIdServerInterceptor(ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);
        _correlationIdAccessor = correlationIdAccessor;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using (_correlationIdAccessor.EnterScope(context.RequestHeaders.GetValue(MetadataKey)))
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
    }
}
