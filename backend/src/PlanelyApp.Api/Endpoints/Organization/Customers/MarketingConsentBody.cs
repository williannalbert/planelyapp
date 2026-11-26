namespace PlanelyApp.Api.Endpoints.Organization.Customers;

/// <summary>
/// The body of <c>PUT /customers/{customerId}/marketing-consent</c>. It is nullable on purpose: a body that leaves it out must be a
/// 400, not a <c>false</c> that withdraws a consent nobody withdrew.
/// </summary>
internal sealed record MarketingConsentBody(bool? AcceptsMarketing = null);