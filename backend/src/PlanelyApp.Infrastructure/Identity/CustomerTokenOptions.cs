using System.Text;

namespace PlanelyApp.Infrastructure.Identity;

public sealed class CustomerTokenOptions
{
    private const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "planelyapp";

    public string Audience { get; set; } = "planelyapp-customers";

    /// <summary>The secret that signs and validates the tokens: at least 32 bytes. It comes from configuration, never from the code.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(15);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(Audience);
        ArgumentOutOfRangeException.ThrowIfLessThan(Encoding.UTF8.GetByteCount(SigningKey), MinimumKeyBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Lifetime, TimeSpan.Zero);
    }
}