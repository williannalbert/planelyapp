using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Customers.Get;

public sealed record GetCustomerQuery(Guid CustomerId) : IQuery<CustomerDto>;