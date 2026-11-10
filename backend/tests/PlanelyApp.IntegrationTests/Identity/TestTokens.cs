using System.Text;
using Microsoft.IdentityModel.Tokens;
using PlanelyApp.Infrastructure.Identity;

namespace PlanelyApp.IntegrationTests.Identity;

internal static class TestTokens
{
    // Not a secret: it only signs the tokens of the tests.
    private const string Key = "test-only-key-for-customer-tokens-0123456789";

    public static CustomerTokenOptions Options { get; } = NewOptions();

    public static CustomerTokenOptions NewOptions(string key = Key) => new() { SigningKey = key };

    /// <summary>What the Api will check in the token (5.8): signature, issuer, audience and lifetime.</summary>
    public static TokenValidationParameters Validation(CustomerTokenOptions options) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
    };
}