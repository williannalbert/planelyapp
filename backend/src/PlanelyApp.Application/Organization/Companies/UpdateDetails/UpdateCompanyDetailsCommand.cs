using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Companies.UpdateDetails;

/// <summary>Changes the data of the company of the request. The CNPJ never changes.</summary>
public sealed record UpdateCompanyDetailsCommand(
    string TradeName,
    string LegalName,
    string Email,
    string Phone,
    string DefaultLanguage) : ICommand<Unit>;