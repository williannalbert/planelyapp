using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.UnlinkUser;

/// <summary>Removes the link to the user. Nothing happens if there was none.</summary>
public sealed record UnlinkProfessionalUserCommand(Guid ProfessionalId) : ICommand<Unit>;