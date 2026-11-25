using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Application.Organization.Users.ResolveActor;

/// <summary>Who is the user behind this Keycloak subject. The Api asks it once per request, before any other use case.</summary>
public sealed record ResolveStaffActorQuery(string IdentityId) : IQuery<StaffActor>;

/// <summary>What the Api needs to know about the user who is acting. The company is null only for the platform administrator.</summary>
public sealed record StaffActor(Guid UserId, Guid? CompanyId, UserRole Role);