using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Companies.Activate;

public sealed record ActivateCompanyCommand : ICommand<Unit>;