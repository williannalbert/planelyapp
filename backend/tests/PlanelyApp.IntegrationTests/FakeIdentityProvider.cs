using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.IntegrationTests;

public sealed record FakeAccount(IdentityProfile Profile, bool Enabled);

/// <summary>An identity provider in memory, with the rule that matters: an e-mail belongs to one account in the whole realm.</summary>
public sealed class FakeIdentityProvider : IIdentityProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FakeAccount> _accounts = [];

    public IReadOnlyDictionary<string, FakeAccount> Accounts
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, FakeAccount>(_accounts);
            }
        }
    }

    public Task<Result<string>> CreateUserAsync(IdentityProfile profile, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_accounts.Values.Any(account => account.Profile.Email.Equals(profile.Email)))
            {
                return Task.FromResult<Result<string>>(Error.Conflict("Identity.EmailAlreadyInUse"));
            }

            var identityId = $"fake-{Guid.NewGuid():N}";
            _accounts[identityId] = new FakeAccount(profile, true);

            return Task.FromResult(Result<string>.Success(identityId));
        }
    }

    public Task<Result<Unit>> UpdateUserAsync(string identityId, IdentityProfile profile, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var current = _accounts.GetValueOrDefault(identityId)
                ?? throw new InvalidOperationException($"No account {identityId}.");

            if (_accounts.Any(pair => pair.Key != identityId && pair.Value.Profile.Email.Equals(profile.Email)))
            {
                return Task.FromResult<Result<Unit>>(Error.Conflict("Identity.EmailAlreadyInUse"));
            }

            _accounts[identityId] = current with { Profile = profile };

            return Task.FromResult(Result.Success());
        }
    }

    public Task SetEnabledAsync(string identityId, bool enabled, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var current = _accounts.GetValueOrDefault(identityId)
                ?? throw new InvalidOperationException($"No account {identityId}.");
            _accounts[identityId] = current with { Enabled = enabled };
        }

        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(string identityId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _accounts.Remove(identityId);
        }

        return Task.CompletedTask;
    }
}