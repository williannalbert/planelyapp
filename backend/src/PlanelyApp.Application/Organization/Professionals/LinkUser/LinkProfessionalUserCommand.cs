using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.LinkUser;

/// <summary>
/// Links the professional to the login of a user of the company, so the user sees the agenda of the professional.
/// Linking another user replaces the link.
/// </summary>
public sealed record LinkProfessionalUserCommand(Guid ProfessionalId, Guid UserId) : ICommand<Unit>;