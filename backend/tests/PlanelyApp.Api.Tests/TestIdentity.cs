using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PlanelyApp.Infrastructure.Identity;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// A Keycloak made of one RSA key: it signs the tokens of the tests and publishes the public half, so the Api validates
/// them as it would validate the real ones, with no network. The keys are made once; generating one costs about as much
/// as the rest of a test.
/// </summary>
internal static class TestIdentity
{
    public const string Subject = "kc-user-1";

    public const string Authority = "http://keycloak.test/realms/planelyapp";

    public const string Audience = "planelyapp-api";

    private const string KeyId = "test-key";

    private static readonly RSA SigningRsa = RSA.Create(2048);

    private static readonly RSA OtherRsa = RSA.Create(2048);

    public static KeycloakOptions Keycloak { get; } = new()
    {
        BaseUrl = "http://keycloak.test",
        Realm = "planelyapp",
        ClientId = Audience,
        ClientSecret = "test-secret",
    };

    /// <summary>What the Api would download from the realm: the issuer and the public key.</summary>
    public static IConfigurationManager<OpenIdConnectConfiguration> Metadata { get; } = BuildMetadata();

    public static SigningCredentials TrustedKey { get; } =
        new(new RsaSecurityKey(SigningRsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);

    /// <summary>The same key id as the trusted one, other key material: the signature cannot be right.</summary>
    public static SigningCredentials StrangerKey { get; } =
        new(new RsaSecurityKey(OtherRsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);

    /// <summary>The attack of signing with a secret the server knows nothing about (and, in a weak setup, with the public key).</summary>
    public static SigningCredentials SharedSecret { get; } =
        new(new SymmetricSecurityKey(new byte[32].Select((_, index) => (byte)(index + 1)).ToArray()), SecurityAlgorithms.HmacSha256);

    /// <summary>A token as Keycloak would issue it. Every parameter can be broken, one at a time, by a test.</summary>
    public static string Token(
        string? subject = Subject,
        string? issuer = Authority,
        string? audience = Audience,
        TimeSpan? lifetime = null,
        SigningCredentials? credentials = null)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(10));

        // A token that is already over was issued in the past; the handler refuses an expiry before the start.
        var notBefore = expires <= now ? expires.AddHours(-1) : now.AddMinutes(-1);

        var claims = new Dictionary<string, object>();

        if (subject is not null)
        {
            claims["sub"] = subject;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            Claims = claims,
            SigningCredentials = credentials ?? TrustedKey,
        });
    }

    private static StaticConfigurationManager<OpenIdConnectConfiguration> BuildMetadata()
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = Authority };
        configuration.SigningKeys.Add(new RsaSecurityKey(SigningRsa.ExportParameters(false)) { KeyId = KeyId });

        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }
}
