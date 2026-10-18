using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Customers;

public sealed record CustomerRegistered(Guid CustomerId, Guid CompanyId) : DomainEvent;

public sealed record CustomerUpdated(Guid CustomerId, Guid CompanyId) : DomainEvent;