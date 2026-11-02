using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>
/// The accounts people log in with (Keycloak). The Application does not know Keycloak: the Infrastructure implements this.
/// What the caller can act on comes back as a failed <see cref="Result{TValue}"/> (an e-mail already in use is a
/// <c>Conflict</c>); a provider that cannot be reached throws.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>Creates the account, asks the person to set a password, and returns its subject (the id to keep).</summary>
    Task<Result<string>> CreateUserAsync(IdentityProfile profile, CancellationToken cancellationToken);

    Task<Result<Unit>> UpdateUserAsync(string identityId, IdentityProfile profile, CancellationToken cancellationToken);

    /// <summary>A disabled account cannot log in. Doing it again is harmless.</summary>
    Task SetEnabledAsync(string identityId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Removes the account. It is the undo of a creation that could not be completed.</summary>
    Task DeleteUserAsync(string identityId, CancellationToken cancellationToken);
}