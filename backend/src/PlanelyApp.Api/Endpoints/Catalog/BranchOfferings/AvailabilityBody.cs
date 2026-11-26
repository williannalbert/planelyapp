namespace PlanelyApp.Api.Endpoints.Catalog.BranchOfferings;

/// <summary>
/// The body of <c>PUT .../availability</c>. It is nullable on purpose: a body that leaves it out must be a 400, not a
/// <c>false</c> that takes the service off the branch.
/// </summary>
internal sealed record AvailabilityBody(bool? IsAvailable = null);