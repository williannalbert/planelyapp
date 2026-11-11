using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Companies.Deactivate;

public sealed record DeactivateCompanyCommand : ICommand<Unit>;