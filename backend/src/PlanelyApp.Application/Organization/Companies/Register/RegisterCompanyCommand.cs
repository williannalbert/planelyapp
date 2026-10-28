using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Companies.Register;

/// <summary>Registers a company. The only use case of the module that runs without a company in the request.</summary>
public sealed record RegisterCompanyCommand(
    string TradeName,
    string LegalName,
    string Cnpj,
    string Email,
    string Phone,
    string DefaultLanguage,
    BookingPolicyData? BookingPolicy = null) : ICommand<Guid>;