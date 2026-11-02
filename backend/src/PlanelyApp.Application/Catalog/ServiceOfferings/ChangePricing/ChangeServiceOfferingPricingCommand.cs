using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.ChangePricing;

/// <summary>
/// Makes the service fixed price with the given base price, or on request. A service that becomes on request keeps the
/// prices its branches gave it, but they do not count while it is on request (<c>PriceCalculator</c>).
/// </summary>
public sealed record ChangeServiceOfferingPricingCommand(Guid ServiceOfferingId, PricingData Pricing) : ICommand<Unit>;