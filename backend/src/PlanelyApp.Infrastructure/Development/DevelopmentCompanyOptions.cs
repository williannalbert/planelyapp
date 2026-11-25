namespace PlanelyApp.Infrastructure.Development;

/// <summary>The company of the development environment and its administrator, who is a user of the realm. Values of development only.</summary>
public sealed class DevelopmentCompanyOptions
{
    public string TradeName { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string Cnpj { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string DefaultLanguage { get; set; } = "pt-BR";

    /// <summary>The <c>sub</c> of the token of the realm user who administers the company.</summary>
    public string AdminIdentityId { get; set; } = string.Empty;

    public string AdminName { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TradeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Cnpj);
        ArgumentException.ThrowIfNullOrWhiteSpace(Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(Phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(DefaultLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(AdminIdentityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(AdminName);
        ArgumentException.ThrowIfNullOrWhiteSpace(AdminEmail);
    }
}