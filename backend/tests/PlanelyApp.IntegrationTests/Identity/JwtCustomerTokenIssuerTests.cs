using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Infrastructure.Identity;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Identity;

public class JwtCustomerTokenIssuerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    private readonly CustomerTokenOptions _options = TestTokens.NewOptions();
    private readonly JsonWebTokenHandler _handler = new();

    [Fact]
    public async Task The_token_should_be_valid_and_say_who_and_where()
    {
        var now = DateTime.UtcNow;

        var token = new JwtCustomerTokenIssuer(_options).Issue(CompanyId, CustomerId, now);

        var result = await _handler.ValidateTokenAsync(token.Value, TestTokens.Validation(_options));
        result.IsValid.ShouldBeTrue();
        result.Claims[JwtRegisteredClaimNames.Sub].ShouldBe(CustomerId.ToString());
        result.Claims[CustomerTokenClaims.CompanyId].ShouldBe(CompanyId.ToString());
        result.Claims[CustomerTokenClaims.ActorKind].ShouldBe(CustomerTokenClaims.CustomerKind);
        token.ExpiresAtUtc.ShouldBe(now + _options.Lifetime);
    }

    [Fact]
    public async Task A_token_issued_long_ago_should_be_expired()
    {
        var token = new JwtCustomerTokenIssuer(_options).Issue(CompanyId, CustomerId, DateTime.UtcNow.AddHours(-1));

        var result = await _handler.ValidateTokenAsync(token.Value, TestTokens.Validation(_options));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task A_token_signed_with_another_key_should_be_refused()
    {
        var other = TestTokens.NewOptions("another-test-only-key-for-the-tokens-0123456");
        var token = new JwtCustomerTokenIssuer(other).Issue(CompanyId, CustomerId, DateTime.UtcNow);

        var result = await _handler.ValidateTokenAsync(token.Value, TestTokens.Validation(_options));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Every_token_should_be_different()
    {
        var issuer = new JwtCustomerTokenIssuer(_options);
        var now = DateTime.UtcNow;

        issuer.Issue(CompanyId, CustomerId, now).Value.ShouldNotBe(issuer.Issue(CompanyId, CustomerId, now).Value);
    }

    [Fact]
    public void A_time_that_is_not_UTC_should_be_refused()
    {
        var issuer = new JwtCustomerTokenIssuer(_options);

        Should.Throw<ArgumentException>(
            () => issuer.Issue(CompanyId, CustomerId, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)));
    }

    [Theory]
    [InlineData("", "planelyapp-customers", 32, 15)]
    [InlineData("planelyapp", "", 32, 15)]
    [InlineData("planelyapp", "planelyapp-customers", 31, 15)]
    [InlineData("planelyapp", "planelyapp-customers", 32, 0)]
    public void Invalid_options_should_be_refused(string issuer, string audience, int keyLength, int lifetimeMinutes) =>
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddCustomerTokenIssuer(new CustomerTokenOptions
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = new string('k', keyLength),
            Lifetime = TimeSpan.FromMinutes(lifetimeMinutes),
        }));
}