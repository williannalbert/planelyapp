using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Users.Deactivate;

public sealed record DeactivateUserCommand(Guid UserId) : ICommand<Unit>;