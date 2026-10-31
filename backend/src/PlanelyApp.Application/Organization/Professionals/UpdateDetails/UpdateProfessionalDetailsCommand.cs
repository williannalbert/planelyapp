using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.UpdateDetails;

public sealed record UpdateProfessionalDetailsCommand(Guid ProfessionalId, string Name, string? Specialty)
    : ICommand<Unit>;