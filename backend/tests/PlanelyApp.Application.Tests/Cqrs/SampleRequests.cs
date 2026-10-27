using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Tests.Cqrs;

internal sealed record GreetCommand(string Name) : ICommand<string>;

internal sealed record CountCommand(int Start) : ICommand<int>;

internal sealed record RecordCommand : ICommand<Unit>;

internal sealed record WhoAmICommand : ICommand<Guid>;

internal sealed record GreetingQuery(string Name) : IQuery<string>;

internal sealed class GreetHandler : ICommandHandler<GreetCommand, string>
{
    public Task<Result<string>> HandleAsync(GreetCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Task.FromResult(Result<string>.Failure(Error.Validation("Greeting.NameRequired")));
        }

        return Task.FromResult(Result<string>.Success($"Hello, {command.Name}!"));
    }
}

internal sealed class CountHandler : ICommandHandler<CountCommand, int>
{
    public Task<Result<int>> HandleAsync(CountCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result<int>.Success(command.Start + 1));
}

internal sealed class RecordingHandler : ICommandHandler<RecordCommand, Unit>
{
    public int Calls { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Task<Result<Unit>> HandleAsync(RecordCommand command, CancellationToken cancellationToken)
    {
        Calls++;
        ReceivedToken = cancellationToken;

        return Task.FromResult(Result.Success());
    }
}

/// <summary>Something that lives as long as one scope (one request).</summary>
internal sealed class ScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal sealed class WhoAmIHandler(ScopeMarker marker) : ICommandHandler<WhoAmICommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(WhoAmICommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result<Guid>.Success(marker.Id));
}

internal sealed class GreetingQueryHandler : IQueryHandler<GreetingQuery, string>
{
    public Task<Result<string>> HandleAsync(GreetingQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(query.Name == "nobody"
            ? Result<string>.Failure(Error.NotFound("Greeting.NotFound"))
            : Result<string>.Success($"Hi, {query.Name}."));
}