namespace PlanelyApp.Api.Endpoints.Organization.Customers;

/// <summary>The body of <c>PUT /customers/{customerId}/contact</c>. It replaces the contacts: one left out is removed.</summary>
internal sealed record CustomerContactBody(string? Email = null, string? Phone = null);