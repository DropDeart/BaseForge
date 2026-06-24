using MediatR;

namespace BaseForge.Core.CQRS;

/// <summary>
/// Değer döndürmeyen bir komutu (durum değiştiren işlem) işaretler. MediatR üzerine kuruludur.
/// </summary>
public interface ICommand : IRequest;

/// <summary>
/// <typeparamref name="TResponse"/> tipinde bir sonuç döndüren komutu işaretler.
/// </summary>
/// <typeparam name="TResponse">Komutun döndürdüğü sonuç tipi.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>;
