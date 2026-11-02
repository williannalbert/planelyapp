using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.BranchOfferings.SetAvailability;

/// <summary>Whether the branch offers the service (RN14). A branch offers every service until it says otherwise.</summary>
public sealed record SetBranchOfferingAvailabilityCommand(Guid BranchId, Guid ServiceOfferingId, bool IsAvailable)
    : ICommand<Unit>;