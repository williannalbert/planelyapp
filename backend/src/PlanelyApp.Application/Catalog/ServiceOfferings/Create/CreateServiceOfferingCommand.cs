using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Create;

/// <summary>Creates a service in the company of the request. The name is unique inside the company.</summary>
public sealed record CreateServiceOfferingCommand(
    string Name,
    int DurationMinutes,
    PricingData Pricing,
    string? Description = null,
    Guid? CategoryId = null) : ICommand<Guid>;