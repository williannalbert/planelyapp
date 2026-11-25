using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.ResolveActor;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// Answers the one query the authentication asks (who is the user of this identity) and records what was asked.
/// The use case itself is tested in the Application; here the question is only what the Api does with the answer.
/// </summary>
internal sealed class FakeDispatcher : IDispatcher
{
    private readonly List<string> _asked = [];

    /// <summary>By default nobody is registered.</summary>
    public Func<string, Result<StaffActor>> Resolve { get; set; } = _ => Error.Forbidden("User.NotRegistered");

    /// <summary>The identities that were looked up, in order.</summary>
    public IReadOnlyList<string> Asked => _asked;

    public Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        if (query is not ResolveStaffActorQuery resolve)
        {
            throw new NotSupportedException($"The test did not expect {query.GetType().Name}.");
        }

        _asked.Add(resolve.IdentityId);

        return Task.FromResult((Result<TResponse>)(object)Resolve(resolve.IdentityId));
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"The test did not expect {command.GetType().Name}.");
}
