using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Abstractions.Decorators;

/// <summary>
/// The only place that saves. <c>SaveChanges</c> is one database transaction, so everything the handler and the
/// event handlers did is saved together or not at all. A failed result or an exception saves nothing.
/// </summary>
internal sealed class TransactionCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IUnitOfWork unitOfWork) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}