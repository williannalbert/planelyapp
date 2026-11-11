using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Identity.AccessCodes.Request;

internal sealed class RequestAccessCodeHandler(
    IAccessCodeRepository accessCodes,
    IAccessCodeSender sender,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ICommandHandler<RequestAccessCodeCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(RequestAccessCodeCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var destination = AccessDestination.From(command.Channel, command.Destination);

        // The Domain applies the limit of resending (RN22), but only the caller can load the codes of the last hour.
        var recent = await accessCodes.GetCreationTimesSinceAsync(
            companyId, destination.Value, nowUtc - AccessCode.ResendWindow, cancellationToken);

        var issued = AccessCode.Issue(companyId, destination.Channel, destination.Value, recent, nowUtc);
        accessCodes.Add(issued.AccessCode);

        // Before the save, on purpose: if the delivery fails nothing is saved, so the customer does not lose one of the
        // five codes of the hour. The opposite (a code that arrives and a save that fails) costs one more request.
        await sender.SendAsync(
            new AccessCodeDelivery(
                companyId, destination.Channel, destination.Value, issued.PlainCode, issued.AccessCode.ExpiresAtUtc),
            cancellationToken);

        return Result.Success();
    }
}