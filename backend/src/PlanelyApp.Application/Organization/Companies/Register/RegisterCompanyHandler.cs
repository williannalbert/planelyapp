using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Organization.Companies.Register;

internal sealed class RegisterCompanyHandler(ICompanyRepository companies)
    : ICommandHandler<RegisterCompanyCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterCompanyCommand command, CancellationToken cancellationToken)
    {
        var cnpj = Cnpj.Create(command.Cnpj);

        if (await companies.ExistsByCnpjAsync(cnpj, cancellationToken))
        {
            return Error.Conflict("Company.CnpjAlreadyRegistered");
        }

        var company = Company.Create(
            command.TradeName,
            command.LegalName,
            cnpj,
            Email.Create(command.Email),
            Phone.Create(command.Phone),
            Language.Create(command.DefaultLanguage),
            command.BookingPolicy?.ToDomain());

        companies.Add(company);

        return company.Id;
    }
}