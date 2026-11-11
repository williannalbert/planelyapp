using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Cqrs;

public interface IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}