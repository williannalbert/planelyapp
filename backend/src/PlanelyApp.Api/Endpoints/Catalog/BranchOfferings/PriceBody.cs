namespace PlanelyApp.Api.Endpoints.Catalog.BranchOfferings;

/// <summary>
/// The body of <c>PUT .../price</c>. It is nullable on purpose: a body that leaves it out must be a 400, not a <c>0</c> that
/// makes the service free in the branch.
/// </summary>
internal sealed record PriceBody(decimal? Price = null);