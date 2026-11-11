namespace PlanelyApp.Application.Organization.Customers;

/// <summary>Everything about one customer. The CPF comes formatted (000.000.000-00).</summary>
public sealed record CustomerDto(
    Guid Id,
    string? Name,
    string? Email,
    string? Phone,
    string? Cpf,
    DateOnly? BirthDate,
    DateTime? LastAccessAt,
    bool AcceptsMarketing,
    string? PreferredLanguage);