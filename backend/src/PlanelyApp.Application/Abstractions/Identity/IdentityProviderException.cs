namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>
/// The identity provider failed in a way the caller cannot act on: it could not be reached, refused a request or
/// answered something unexpected. The Api answers 502 or 503. What the caller can act on is a failed result instead.
/// </summary>
public sealed class IdentityProviderException : Exception
{
    public IdentityProviderException(string message)
        : base(message)
    {
    }

    public IdentityProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}