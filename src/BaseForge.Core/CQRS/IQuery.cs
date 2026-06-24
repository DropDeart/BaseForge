using MediatR;

namespace BaseForge.Core.CQRS;

/// <summary>
/// Durum değiştirmeden veri okuyan ve <typeparamref name="TResponse"/> döndüren bir sorguyu işaretler.
/// MediatR üzerine kuruludur.
/// </summary>
/// <typeparam name="TResponse">Sorgunun döndürdüğü sonuç tipi.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>;
