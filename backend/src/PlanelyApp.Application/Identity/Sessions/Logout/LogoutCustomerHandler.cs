using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Identity.Sessions.Logout;

internal sealed class LogoutCustomerHandler(
    ICustomerSessionRepository sessions,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<LogoutCustomerCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(LogoutCustomerCommand command, CancellationToken cancellationToken)
    {
        var session = await sessions.GetByRefreshTokenHashAsync(
            tenantProvider.RequireCompanyId(), CustomerSession.HashToken(command.RefreshToken), cancellationToken);

        // Nothing to say about a token that is not there: whoever holds it is already logged out.
        session?.Revoke(timeProvider.GetUtcNow().UtcDateTime);

        return Result.Success();
    }
}