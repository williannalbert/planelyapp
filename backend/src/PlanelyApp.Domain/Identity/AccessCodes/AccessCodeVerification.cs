namespace PlanelyApp.Domain.Identity.AccessCodes;

/// <summary>
/// The outcome of entering a code. It is a value and not an exception because a wrong attempt must be
/// saved (it counts toward the limit), and an exception would undo it.
/// </summary>
public enum AccessCodeVerification
{
    Verified = 0,
    Invalid = 1,
    Expired = 2,
    AlreadyUsed = 3,
    TooManyAttempts = 4
}