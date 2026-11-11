using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.Activate;

public sealed record ActivateProfessionalCommand(Guid ProfessionalId) : ICommand<Unit>;