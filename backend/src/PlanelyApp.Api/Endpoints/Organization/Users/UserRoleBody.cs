using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization.Users;

/// <summary>The body of <c>PUT /users/{userId}/role</c>. Nullable for the same reason as <see cref="CreateUserBody"/>.</summary>
internal sealed record UserRoleBody(UserRole? Role);