using FluentValidation;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Decorators;

internal sealed class ValidationCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var error = await ValidationRunner.ValidateAsync(validators, command, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        return await inner.HandleAsync(command, cancellationToken);
    }
}