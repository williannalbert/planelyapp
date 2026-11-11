using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Users.UpdateDetails;

public sealed record UpdateUserDetailsCommand(Guid UserId, string Name, string Email, string? PreferredLanguage)
    : ICommand<Unit>;