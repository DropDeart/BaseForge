using BaseForge.Core.Logging;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BaseForge.API.Grpc;

/// <summary>
/// Giden gRPC çağrılarının metadata'sına o anki isteğin (<see cref="ICorrelationIdAccessor"/>)
/// correlation id'sini <c>correlation-id</c> olarak ekler — sunucu tarafında
/// <see cref="CorrelationIdServerInterceptor"/> tarafından okunur. Üretilen servisin
/// <c>AddGrpcClient&lt;...&gt;(...).AddInterceptor&lt;CorrelationIdClientInterceptor&gt;()</c>
/// çağrısıyla (BaseForge.CodeGen Program.cs şablonu) her gRPC istemcisine otomatik eklenir.
/// </summary>
public sealed class CorrelationIdClientInterceptor : Interceptor
{
    private const string MetadataKey = "correlation-id";

    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    /// <summary>Yeni bir interceptor örneği oluşturur.</summary>
    /// <param name="correlationIdAccessor">Giden çağrıya eklenecek correlation id'nin okunacağı accessor.</param>
    public CorrelationIdClientInterceptor(ICorrelationIdAccessor correlationIdAccessor)
    {
        ArgumentNullException.ThrowIfNull(correlationIdAccessor);
        _correlationIdAccessor = correlationIdAccessor;
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithCorrelationId(context));
    }

    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, WithCorrelationId(context));
    }

    private ClientInterceptorContext<TRequest, TResponse> WithCorrelationId<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var correlationId = _correlationIdAccessor.Current;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return context;
        }

        var headers = new Metadata();
        foreach (var entry in context.Options.Headers ?? [])
        {
            headers.Add(entry);
        }

        headers.Add(MetadataKey, correlationId);

        var options = context.Options.WithHeaders(headers);
        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
    }
}
