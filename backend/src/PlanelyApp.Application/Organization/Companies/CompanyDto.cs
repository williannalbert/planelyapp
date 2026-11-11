namespace PlanelyApp.Application.Organization.Companies;

public sealed record CompanyDto(
    Guid Id,
    string TradeName,
    string LegalName,
    string Cnpj,
    string Email,
    string Phone,
    string DefaultLanguage,
    BookingPolicyData BookingPolicy,
    bool IsActive);