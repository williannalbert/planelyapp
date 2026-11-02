using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;

/// <summary>Deactivating is how a service goes away: nothing is deleted. Professionals and promotions that cite it are not touched.</summary>
public sealed record DeactivateServiceOfferingCommand(Guid ServiceOfferingId) : ICommand<Unit>;