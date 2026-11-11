namespace PlanelyApp.Application.Organization.Customers;

/// <summary>What a list needs. No CPF and no birth date: a list does not need personal data it does not show.</summary>
public sealed record CustomerSummaryDto(
    Guid Id,
    string? Name,
    string? Email,
    string? Phone,
    bool AcceptsMarketing,
    DateTime? LastAccessAt);