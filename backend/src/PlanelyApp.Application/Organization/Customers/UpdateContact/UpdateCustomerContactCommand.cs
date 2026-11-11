using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Customers.UpdateContact;

/// <summary>Replaces the contacts of the customer. A blank one is removed, but at least one must remain.</summary>
public sealed record UpdateCustomerContactCommand(Guid CustomerId, string? Email, string? Phone) : ICommand<Unit>;