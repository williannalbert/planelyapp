namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>The body of <c>PUT /professionals/{professionalId}/offerings/{serviceOfferingId}</c>. <c>{}</c> means the standard duration of the service.</summary>
internal sealed record OfferingBody(int? CustomDurationMinutes = null);