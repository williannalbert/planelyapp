namespace PlanelyApp.Domain.Identity.AccessCodes;

/// <summary>
/// A new access code and the code to send to the customer. The code in plain text exists only here:
/// it is never saved.
/// </summary>
public sealed record IssuedAccessCode(AccessCode AccessCode, string PlainCode)
{
    // The default ToString would print the secret in any log that prints the object.
    public override string ToString() => $"{nameof(IssuedAccessCode)} {{ Id = {AccessCode.Id}, PlainCode = *** }}";
}