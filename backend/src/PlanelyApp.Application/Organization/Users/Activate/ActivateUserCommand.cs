using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Users.Activate;

public sealed record ActivateUserCommand(Guid UserId) : ICommand<Unit>;