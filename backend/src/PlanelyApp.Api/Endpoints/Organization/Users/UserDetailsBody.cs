namespace PlanelyApp.Api.Endpoints.Organization.Users;

/// <summary>The body of <c>PUT /users/{userId}</c>. No id: the user is the one of the route.</summary>
internal sealed record UserDetailsBody(string Name, string Email, string? PreferredLanguage = null);