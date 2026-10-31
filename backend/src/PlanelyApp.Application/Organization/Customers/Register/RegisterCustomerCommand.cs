using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Customers.Register;

/// <summary>
/// Registers a customer in the company of the request, usually the receptionist at the counter.
/// At least one of <paramref name="Email"/> and <paramref name="Phone"/> is needed; a blank one counts as not informed.
/// </summary>
public sealed record RegisterCustomerCommand(
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    bool AcceptsMarketing = false,
    string? PreferredLanguage = null) : ICommand<Guid>;