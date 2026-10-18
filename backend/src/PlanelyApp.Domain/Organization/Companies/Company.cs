using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Companies;

/// <summary>
/// A company (tenant). Its Id is the CompanyId that every other aggregate carries.
/// </summary>
public sealed class Company : AggregateRoot
{
    private const int TradeNameMaxLength = 150;
    private const int LegalNameMaxLength = 200;

    private Company(
        string tradeName,
        string legalName,
        Cnpj cnpj,
        Email email,
        Phone phone,
        Language defaultLanguage,
        BookingPolicy bookingPolicy)
    {
        TradeName = tradeName;
        LegalName = legalName;
        Cnpj = cnpj;
        Email = email;
        Phone = phone;
        DefaultLanguage = defaultLanguage;
        BookingPolicy = bookingPolicy;
        IsActive = true;
    }

    public string TradeName { get; private set; }

    public string LegalName { get; private set; }

    /// <summary>Never changes after the company is registered.</summary>
    public Cnpj Cnpj { get; }

    public Email Email { get; private set; }

    public Phone Phone { get; private set; }

    public Language DefaultLanguage { get; private set; }

    public BookingPolicy BookingPolicy { get; private set; }

    public bool IsActive { get; private set; }

    public static Company Create(
        string? tradeName,
        string? legalName,
        Cnpj cnpj,
        Email email,
        Phone phone,
        Language defaultLanguage,
        BookingPolicy? bookingPolicy = null)
    {
        return new Company(
            Guard.RequiredText(tradeName, TradeNameMaxLength, "Company.TradeNameInvalid"),
            Guard.RequiredText(legalName, LegalNameMaxLength, "Company.LegalNameInvalid"),
            cnpj,
            email,
            phone,
            defaultLanguage,
            bookingPolicy ?? BookingPolicy.Default);
    }

    public void UpdateDetails(
        string? tradeName,
        string? legalName,
        Email email,
        Phone phone,
        Language defaultLanguage)
    {
        var validTradeName = Guard.RequiredText(tradeName, TradeNameMaxLength, "Company.TradeNameInvalid");
        var validLegalName = Guard.RequiredText(legalName, LegalNameMaxLength, "Company.LegalNameInvalid");

        TradeName = validTradeName;
        LegalName = validLegalName;
        Email = email;
        Phone = phone;
        DefaultLanguage = defaultLanguage;
    }

    public void UpdatePolicy(BookingPolicy bookingPolicy) => BookingPolicy = bookingPolicy;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}