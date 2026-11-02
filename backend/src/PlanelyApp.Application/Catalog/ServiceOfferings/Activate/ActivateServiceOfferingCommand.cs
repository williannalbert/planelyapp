using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Activate;

public sealed record ActivateServiceOfferingCommand(Guid ServiceOfferingId) : ICommand<Unit>;