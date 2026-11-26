namespace PlanelyApp.Api.Endpoints.Catalog.Pricing;

/// <summary>
/// The body of <c>POST .../price-quotes</c>: the moment (UTC) and the services of the appointment, in the order asked. The branch is
/// the one of the route. A list left out is the same as an empty one, which the validator refuses.
/// </summary>
internal sealed record PriceQuoteBody(DateTime StartUtc, IReadOnlyList<Guid>? ServiceIds = null);