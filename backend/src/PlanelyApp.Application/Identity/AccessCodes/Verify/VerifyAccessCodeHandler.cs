using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Application.Identity.Sessions;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Identity.AccessCodes.Verify;

internal sealed class VerifyAccessCodeHandler(
    IAccessCodeRepository accessCodes,
    ICustomerRepository customers,
    ICustomerSessionRepository sessions,
    ICustomerTokenIssuer tokens,
    CustomerSessionOptions options,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<VerifyAccessCodeCommand, VerifyAccessCodeResponse>
{
    public async Task<Result<VerifyAccessCodeResponse>> HandleAsync(
        VerifyAccessCodeCommand command,
        CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var destination = AccessDestination.From(command.Channel, command.Destination);

        var accessCode = await accessCodes.GetLatestAsync(companyId, destination.Value, cancellationToken);

        // Nobody asked for a code for this destination: the same answer as a wrong code, so it does not say who exists.
        if (accessCode is null)
        {
            return new VerifyAccessCodeResponse(AccessCodeVerification.Invalid, null);
        }

        // A wrong attempt changes the access code, and this is a success result, so the transaction saves it (RN21).
        var outcome = accessCode.Verify(command.Code, nowUtc);

        if (outcome != AccessCodeVerification.Verified)
        {
            return new VerifyAccessCodeResponse(outcome, null);
        }

        var (customer, isNew) = await FindOrRegisterAsync(companyId, destination, cancellationToken);
        customer.RegisterAccess(nowUtc);

        var opened = CustomerSession.Open(companyId, customer.Id, options.SessionLifetime, nowUtc);
        sessions.Add(opened.Session);

        var accessToken = tokens.Issue(companyId, customer.Id, nowUtc);

        return new VerifyAccessCodeResponse(
            outcome,
            new CustomerLogin(
                customer.Id,
                isNew,
                accessToken.Value,
                accessToken.ExpiresAtUtc,
                opened.RefreshToken,
                opened.Session.ExpiresAtUtc));
    }

    // RN24: e-mail and phone identify the customer inside the company. The first access creates the record with only
    // the contact used to log in; the name comes later.
    private async Task<(Customer Customer, bool IsNew)> FindOrRegisterAsync(
        Guid companyId,
        AccessDestination destination,
        CancellationToken cancellationToken)
    {
        var existing = destination.Email is { } email
            ? await customers.FindByEmailAsync(companyId, email, cancellationToken)
            : await customers.FindByPhoneAsync(companyId, destination.Phone!, cancellationToken);

        if (existing is not null)
        {
            return (existing, false);
        }

        var customer = Customer.Register(companyId, destination.Email, destination.Phone);
        customers.Add(customer);

        return (customer, true);
    }
}