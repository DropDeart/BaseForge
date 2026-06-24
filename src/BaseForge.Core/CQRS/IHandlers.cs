using MediatR;

namespace BaseForge.Core.CQRS;

/// <summary>
/// Değer döndürmeyen bir <see cref="ICommand"/> işleyicisi.
/// </summary>
/// <typeparam name="TCommand">İşlenen komut tipi.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;

/// <summary>
/// <typeparamref name="TResponse"/> döndüren bir <see cref="ICommand{TResponse}"/> işleyicisi.
/// </summary>
/// <typeparam name="TCommand">İşlenen komut tipi.</typeparam>
/// <typeparam name="TResponse">Komutun döndürdüğü sonuç tipi.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

/// <summary>
/// Bir <see cref="IQuery{TResponse}"/> işleyicisi.
/// </summary>
/// <typeparam name="TQuery">İşlenen sorgu tipi.</typeparam>
/// <typeparam name="TResponse">Sorgunun döndürdüğü sonuç tipi.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
