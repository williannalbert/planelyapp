using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.BranchOfferings.ClearPrice;

/// <summary>The branch goes back to the base price of the service. Does nothing if it had no price of its own.</summary>
public sealed record ClearBranchOfferingPriceCommand(Guid BranchId, Guid ServiceOfferingId) : ICommand<Unit>;