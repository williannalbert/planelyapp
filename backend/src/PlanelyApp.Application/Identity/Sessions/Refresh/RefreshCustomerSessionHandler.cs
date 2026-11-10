using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Identity.Sessions.Refresh;

internal sealed class RefreshCustomerSessionHandler(
    ICustomerSessionRepository sessions,
    ICustomerTokenIssuer tokens,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<RefreshCustomerSessionCommand, CustomerTokens>
{
    public async Task<Result<CustomerTokens>> HandleAsync(
        RefreshCustomerSessionCommand command,
        CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        var session = await sessions.GetByRefreshTokenHashAsync(
            companyId, CustomerSession.HashToken(command.RefreshToken), cancellationToken);

        // Unknown, revoked and expired look the same on purpose, and so does a token that was already renewed.
        if (session is null || !session.IsActive(nowUtc))
        {
            return Error.Unauthorized("CustomerSession.RefreshTokenInvalid");
        }

        var refreshToken = session.Renew(nowUtc);
        var accessToken = tokens.Issue(companyId, session.CustomerId, nowUtc);

        return new CustomerTokens(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken, session.ExpiresAtUtc);
    }
}