using PlanelyApp.Domain.Identity.AccessCodes;

namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>
/// Delivers the access code to the customer (e-mail or SMS). A port, and not a message of the outbox: the outbox keeps its
/// payload in the database, and the code is a secret. It throws <see cref="AccessCodeDeliveryException"/> when it cannot.
/// </summary>
public interface IAccessCodeSender
{
    Task SendAsync(AccessCodeDelivery delivery, CancellationToken cancellationToken);
}

public sealed record AccessCodeDelivery(
    Guid CompanyId,
    AccessChannel Channel,
    string Destination,
    string Code,
    DateTime ExpiresAtUtc)
{
    // The default ToString would print the secret in any log that prints the object.
    public override string ToString() =>
        $"{nameof(AccessCodeDelivery)} {{ CompanyId = {CompanyId}, Channel = {Channel}, Code = *** }}";
}