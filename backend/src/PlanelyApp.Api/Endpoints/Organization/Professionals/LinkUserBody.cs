namespace PlanelyApp.Api.Endpoints.Organization.Professionals;

/// <summary>The body of <c>PUT /professionals/{professionalId}/user</c>. The user is what is being linked, so it is the one id that travels in a body.</summary>
internal sealed record LinkUserBody(Guid UserId);