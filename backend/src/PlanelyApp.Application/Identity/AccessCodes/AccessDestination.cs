using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Identity.AccessCodes;

/// <summary>The e-mail or phone of the customer, normalized by the Domain: the form the access codes are stored and found by.</summary>
internal sealed record AccessDestination(AccessChannel Channel, string Value, Email? Email, Phone? Phone)
{
    public static AccessDestination From(AccessChannel channel, string? text)
    {
        if (channel == AccessChannel.Email)
        {
            var email = Email.Create(text);

            return new AccessDestination(channel, email.Value, email, null);
        }

        var phone = Phone.Create(text);

        return new AccessDestination(channel, phone.Value, null, phone);
    }
}