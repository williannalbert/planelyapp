namespace PlanelyApp.Application.Organization.Customers.Events;

// Only the id. The consumer reads the customer from the Api, so no name, e-mail, phone, CPF or consent travels (LGPD).
internal sealed record CustomerRegisteredMessage(Guid CustomerId);

internal sealed record CustomerUpdatedMessage(Guid CustomerId);