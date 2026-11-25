using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Users.ResolveActor;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// Stands in for the Application. It answers the query the authentication asks (who is the user of this identity), and the
/// requests an endpoint sends when the test has said how to answer them; every other request is a failure of the test.
/// The use cases themselves are tested in the Application; here the question is only what the Api does with the request
/// and with the answer.
/// </summary>
internal sealed class FakeDispatcher : IDispatcher
{
    private readonly List<string> _asked = [];
    private readonly List<object> _requests = [];
    private readonly Dictionary<Type, Func<object, object>> _replies = [];

    /// <summary>By default nobody is registered.</summary>
    public Func<string, Result<StaffActor>> Resolve { get; set; } = _ => Error.Forbidden("User.NotRegistered");

    /// <summary>The identities that were looked up, in order.</summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>What the endpoints sent, in order (the lookup of the user is in <see cref="Asked"/>, not here).</summary>
    public IReadOnlyList<object> Requests => _requests;

    /// <summary>How to answer one kind of request.</summary>
    public FakeDispatcher Reply<TRequest, TResponse>(Func<TRequest, Result<TResponse>> reply)
    {
        _replies[typeof(TRequest)] = request => reply((TRequest)request);

        return this;
    }

    public Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        Answer<TResponse>(query);

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        Answer<TResponse>(command);

    private Task<Result<TResponse>> Answer<TResponse>(object request)
    {
        if (request is ResolveStaffActorQuery resolve)
        {
            _asked.Add(resolve.IdentityId);

            return Task.FromResult((Result<TResponse>)(object)Resolve(resolve.IdentityId));
        }

        _requests.Add(request);

        if (!_replies.TryGetValue(request.GetType(), out var reply))
        {
            throw new NotSupportedException($"The test did not say how to answer {request.GetType().Name}.");
        }

        return Task.FromResult((Result<TResponse>)reply(request));
    }
}
