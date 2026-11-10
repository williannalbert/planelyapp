using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.Identity.AccessCodes;

namespace PlanelyApp.Application.Identity.AccessCodes.Verify;

/// <summary>Checks the code the customer typed. On the first access it also registers the customer (RN24).</summary>
public sealed record VerifyAccessCodeCommand(AccessChannel Channel, string? Destination, string? Code)
    : ICommand<VerifyAccessCodeResponse>
{
    // The default ToString would print the code in any log that prints the command.
    public override string ToString() => $"{nameof(VerifyAccessCodeCommand)} {{ Channel = {Channel}, Code = *** }}";
}