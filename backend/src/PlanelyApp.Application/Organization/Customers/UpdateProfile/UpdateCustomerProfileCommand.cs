using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Customers.UpdateProfile;

/// <summary>Replaces the whole profile: a field left out (or blank) is cleared.</summary>
public sealed record UpdateCustomerProfileCommand(
    Guid CustomerId,
    string? Name,
    string? Cpf,
    DateOnly? BirthDate,
    string? PreferredLanguage) : ICommand<Unit>;