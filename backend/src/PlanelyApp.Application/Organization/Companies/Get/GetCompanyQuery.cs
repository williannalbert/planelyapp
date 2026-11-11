using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Companies.Get;

public sealed record GetCompanyQuery : IQuery<CompanyDto>;