using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlanelyApp.Application.Abstractions.Identity;

namespace PlanelyApp.Infrastructure.Identity;

/// <summary>
/// Signs the access token of the customer as a JWT (HS256). The same application issues and validates it, so one shared
/// secret is enough; a pair of keys would only matter if another service had to validate the token.
/// </summary>
internal sealed class JwtCustomerTokenIssuer : ICustomerTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly CustomerTokenOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtCustomerTokenIssuer(CustomerTokenOptions options)
    {
        _options = options;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public CustomerAccessToken Issue(Guid companyId, Guid customerId, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC.", nameof(nowUtc));
        }

        var expiresAtUtc = nowUtc + _options.Lifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = nowUtc,
            NotBefore = nowUtc,
            Expires = expiresAtUtc,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = customerId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [CustomerTokenClaims.CompanyId] = companyId.ToString(),
                [CustomerTokenClaims.ActorKind] = CustomerTokenClaims.CustomerKind,
            },
            SigningCredentials = _credentials,
        };

        return new CustomerAccessToken(_handler.CreateToken(descriptor), expiresAtUtc);
    }
}