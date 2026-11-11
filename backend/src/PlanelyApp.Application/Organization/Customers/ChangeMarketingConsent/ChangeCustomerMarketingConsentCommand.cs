using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;

/// <summary>Gives or withdraws the marketing consent (LGPD). Appointment notices do not depend on it (RN26).</summary>
public sealed record ChangeCustomerMarketingConsentCommand(Guid CustomerId, bool AcceptsMarketing) : ICommand<Unit>;