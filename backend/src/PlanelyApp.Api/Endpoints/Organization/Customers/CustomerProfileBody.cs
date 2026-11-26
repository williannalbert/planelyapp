namespace PlanelyApp.Api.Endpoints.Organization.Customers;

/// <summary>The body of <c>PUT /customers/{customerId}/profile</c>. It replaces the whole profile: a field left out is cleared.</summary>
internal sealed record CustomerProfileBody(
    string? Name = null,
    string? Cpf = null,
    DateOnly? BirthDate = null,
    string? PreferredLanguage = null);