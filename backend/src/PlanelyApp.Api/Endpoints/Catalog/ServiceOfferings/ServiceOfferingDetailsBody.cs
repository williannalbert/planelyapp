namespace PlanelyApp.Api.Endpoints.Catalog.ServiceOfferings;

/// <summary>
/// The body of <c>PUT /service-offerings/{serviceOfferingId}</c>. No id: the service is the one of the route. It replaces the details,
/// so a category left out takes the service out of its category.
/// </summary>
internal sealed record ServiceOfferingDetailsBody(
    string Name,
    int DurationMinutes,
    string? Description = null,
    Guid? CategoryId = null);