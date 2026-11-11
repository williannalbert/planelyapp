using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.RemoveTimeOff;

public sealed record RemoveProfessionalTimeOffCommand(Guid ProfessionalId, Guid TimeOffId) : ICommand<Unit>;