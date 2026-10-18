using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Customers;

/// <summary>
/// A person served by a company. The same person using two companies has two independent customers.
/// Identified inside the company by phone or e-mail (RN24).
/// </summary>
public sealed class Customer : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 150;
    private static readonly DateOnly EarliestBirthDate = new(1900, 1, 1);

    private Customer(
        Guid companyId,
        string? name,
        Email? email,
        Phone? phone,
        bool acceptsMarketing,
        Language? preferredLanguage)
    {
        CompanyId = companyId;
        Name = name;
        Email = email;
        Phone = phone;
        AcceptsMarketing = acceptsMarketing;
        PreferredLanguage = preferredLanguage;
    }

    public Guid CompanyId { get; }

    /// <summary>Unknown until the customer completes the profile after the first access.</summary>
    public string? Name { get; private set; }

    public Email? Email { get; private set; }

    public Phone? Phone { get; private set; }

    public Cpf? Cpf { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    /// <summary>UTC instant of the last login.</summary>
    public DateTime? LastAccessAt { get; private set; }

    /// <summary>Marketing consent (LGPD). Appointment notices do not depend on it (RN26).</summary>
    public bool AcceptsMarketing { get; private set; }

    public Language? PreferredLanguage { get; private set; }

    public static Customer Register(
        Guid companyId,
        Email? email,
        Phone? phone,
        string? name = null,
        bool acceptsMarketing = false,
        Language? preferredLanguage = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("Customer.CompanyRequired");
        }

        if (email is null && phone is null)
        {
            throw new DomainException("Customer.ContactRequired");
        }

        var customer = new Customer(
            companyId,
            Guard.OptionalText(name, NameMaxLength, "Customer.NameInvalid"),
            email,
            phone,
            acceptsMarketing,
            preferredLanguage);

        customer.AddDomainEvent(new CustomerRegistered(customer.Id, companyId));

        return customer;
    }

    /// <summary>Replaces the contacts. At least one of them must remain.</summary>
    public void UpdateContact(Email? email, Phone? phone)
    {
        if (email is null && phone is null)
        {
            throw new DomainException("Customer.ContactRequired");
        }

        if (object.Equals(Email, email) && object.Equals(Phone, phone))
        {
            return;
        }

        Email = email;
        Phone = phone;
        AddDomainEvent(new CustomerUpdated(Id, CompanyId));
    }

    public void UpdateProfile(string? name, Cpf? cpf, DateOnly? birthDate, Language? preferredLanguage)
    {
        var validName = Guard.OptionalText(name, NameMaxLength, "Customer.NameInvalid");

        if (birthDate is { } date && date < EarliestBirthDate)
        {
            throw new DomainException("Customer.BirthDateInvalid");
        }

        var unchanged = validName == Name
            && object.Equals(Cpf, cpf)
            && birthDate == BirthDate
            && object.Equals(PreferredLanguage, preferredLanguage);

        if (unchanged)
        {
            return;
        }

        Name = validName;
        Cpf = cpf;
        BirthDate = birthDate;
        PreferredLanguage = preferredLanguage;
        AddDomainEvent(new CustomerUpdated(Id, CompanyId));
    }

    public void ChangeMarketingConsent(bool accepts)
    {
        if (AcceptsMarketing == accepts)
        {
            return;
        }

        AcceptsMarketing = accepts;
        AddDomainEvent(new CustomerUpdated(Id, CompanyId));
    }

    public void RegisterAccess(DateTime accessedAtUtc)
    {
        if (accessedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Customer.AccessTimeInvalid");
        }

        LastAccessAt = accessedAtUtc;
    }
}