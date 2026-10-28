using FluentValidation;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Decorators;

internal sealed class ValidationQueryHandler<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        var error = await ValidationRunner.ValidateAsync(validators, query, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        return await inner.HandleAsync(query, cancellationToken);
    }
}