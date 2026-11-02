using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Catalog.ServiceOfferings.Get;

public sealed record GetServiceOfferingQuery(Guid ServiceOfferingId) : IQuery<ServiceOfferingDto>;