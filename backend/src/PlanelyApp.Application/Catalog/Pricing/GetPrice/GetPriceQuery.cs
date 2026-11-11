using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.Pricing.GetPrice;

/// <summary>
/// The price of the services of one appointment in a branch, starting at <see cref="StartUtc"/> (it must be UTC).
/// The same service may come more than once; the lines follow the order of <see cref="ServiceIds"/>.
/// </summary>
public sealed record GetPriceQuery(Guid BranchId, DateTime StartUtc, IReadOnlyList<Guid> ServiceIds)
    : IQuery<PriceQuoteDto>;