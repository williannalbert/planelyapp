using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.UpdateDetails;

/// <summary>Replaces the details of a service. A <see cref="CategoryId"/> left out takes the service out of its category.</summary>
public sealed record UpdateServiceOfferingDetailsCommand(
    Guid ServiceOfferingId,
    string Name,
    int DurationMinutes,
    string? Description = null,
    Guid? CategoryId = null) : ICommand<Unit>;