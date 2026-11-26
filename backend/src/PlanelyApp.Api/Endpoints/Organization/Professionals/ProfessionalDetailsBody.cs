namespace PlanelyApp.Api.Endpoints.Organization.Professionals;

/// <summary>The body of <c>PUT /professionals/{professionalId}</c>. No id: the professional is the one of the route.</summary>
internal sealed record ProfessionalDetailsBody(string Name, string? Specialty = null);