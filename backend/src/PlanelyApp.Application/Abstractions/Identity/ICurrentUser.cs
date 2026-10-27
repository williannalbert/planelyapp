using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>
/// Who is making the request. The Api implements it from the token (phase 5), after turning the Keycloak
/// subject into the id of the <c>User</c>. The company of the operation is not here: it is the one from <c>ITenantProvider</c>.
/// Anonymous requests are refused by the Api before any use case runs, so a handler can rely on all of this;
/// an implementation without an authenticated user throws <see cref="InvalidOperationException"/>.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The id of the <c>User</c> or of the <c>Customer</c>; it is what the Domain receives as the actor.</summary>
    Guid Id { get; }

    ActorKind Kind { get; }

    /// <summary>Only for <see cref="ActorKind.Staff"/>.</summary>
    UserRole? Role { get; }
}