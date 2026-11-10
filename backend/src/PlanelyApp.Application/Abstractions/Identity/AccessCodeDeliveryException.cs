namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>
/// The code could not be delivered. Nothing is saved when this happens, so the attempt does not count against the limit
/// of the customer. The Api answers 502 or 503.
/// </summary>
public sealed class AccessCodeDeliveryException : Exception
{
    public AccessCodeDeliveryException(string message)
        : base(message)
    {
    }

    public AccessCodeDeliveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}