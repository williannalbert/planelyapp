using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Users.Get;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserDto>;