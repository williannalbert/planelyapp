using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.Deactivate;

public sealed record DeactivateProfessionalCommand(Guid ProfessionalId) : ICommand<Unit>;