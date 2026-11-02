using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetPrice;

/// <summary>Gives the service a price of its own in the branch. Only a fixed price service can have one (RN20).</summary>
public sealed record SetBranchOfferingPriceCommand(Guid BranchId, Guid ServiceOfferingId, decimal Price)
    : ICommand<Unit>;