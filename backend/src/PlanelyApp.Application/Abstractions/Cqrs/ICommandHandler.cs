using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Cqrs;

public interface ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}