namespace PlanelyApp.Application.Abstractions.Identity;

/// <summary>Signs the access token of a customer. Who validates it (the Api, 5.8) reads the claims below.</summary>
public interface ICustomerTokenIssuer
{
    CustomerAccessToken Issue(Guid companyId, Guid customerId, DateTime nowUtc);
}

public sealed record CustomerAccessToken(string Value, DateTime ExpiresAtUtc)
{
    public override string ToString() => $"{nameof(CustomerAccessToken)} {{ ExpiresAtUtc = {ExpiresAtUtc:O}, Value = *** }}";
}

/// <summary>The claims of the token, besides the standard <c>sub</c> (the id of the customer).</summary>
public static class CustomerTokenClaims
{
    public const string CompanyId = "company_id";

    public const string ActorKind = "actor_kind";

    public const string CustomerKind = "customer";
}