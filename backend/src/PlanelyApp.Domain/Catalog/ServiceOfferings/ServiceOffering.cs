using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Catalog.ServiceOfferings;

/// <summary>A service a company offers. Fixed price services have a base price; on-request ones do not (RN18).</summary>
public sealed class ServiceOffering : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 150;
    private const int DescriptionMaxLength = 2000;

    private ServiceOffering(
        Guid companyId,
        Guid? categoryId,
        string name,
        string? description,
        TimeSpan standardDuration,
        PricingType pricingType,
        Money? basePrice)
    {
        CompanyId = companyId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        StandardDuration = standardDuration;
        PricingType = pricingType;
        BasePrice = basePrice;
        IsActive = true;
    }

    public Guid CompanyId { get; }

    public Guid? CategoryId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public TimeSpan StandardDuration { get; private set; }

    public PricingType PricingType { get; private set; }

    /// <summary>Present when PricingType is Fixed, null when it is OnRequest.</summary>
    public Money? BasePrice { get; private set; }

    public bool IsActive { get; private set; }

    public static ServiceOffering CreateWithFixedPrice(
        Guid companyId,
        string? name,
        TimeSpan standardDuration,
        Money basePrice,
        string? description = null,
        Guid? categoryId = null) =>
        Create(companyId, name, standardDuration, description, categoryId, PricingType.Fixed, basePrice);

    public static ServiceOffering CreateOnRequest(
        Guid companyId,
        string? name,
        TimeSpan standardDuration,
        string? description = null,
        Guid? categoryId = null) =>
        Create(companyId, name, standardDuration, description, categoryId, PricingType.OnRequest, null);

    public void UpdateDetails(
        string? name,
        string? description,
        TimeSpan standardDuration,
        Guid? categoryId)
    {
        // Validate everything before assigning, so a failure leaves the service untouched.
        var validName = Guard.RequiredText(name, NameMaxLength, "ServiceOffering.NameInvalid");
        var validDescription = Guard.OptionalText(description, DescriptionMaxLength, "ServiceOffering.DescriptionInvalid");
        var validDuration = Guard.DurationInWholeMinutes(standardDuration, "ServiceOffering.DurationInvalid");
        ValidateCategory(categoryId);

        Name = validName;
        Description = validDescription;
        StandardDuration = validDuration;
        CategoryId = categoryId;
    }

    /// <summary>Makes the service fixed price with the given base price (RN18).</summary>
    public void SetFixedPrice(Money basePrice)
    {
        PricingType = PricingType.Fixed;
        BasePrice = basePrice;
    }

    /// <summary>Makes the service on-request: it no longer has a base price (RN18).</summary>
    public void SetOnRequest()
    {
        PricingType = PricingType.OnRequest;
        BasePrice = null;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static ServiceOffering Create(
        Guid companyId,
        string? name,
        TimeSpan standardDuration,
        string? description,
        Guid? categoryId,
        PricingType pricingType,
        Money? basePrice)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("ServiceOffering.CompanyRequired");
        }

        ValidateCategory(categoryId);

        return new ServiceOffering(
            companyId,
            categoryId,
            Guard.RequiredText(name, NameMaxLength, "ServiceOffering.NameInvalid"),
            Guard.OptionalText(description, DescriptionMaxLength, "ServiceOffering.DescriptionInvalid"),
            Guard.DurationInWholeMinutes(standardDuration, "ServiceOffering.DurationInvalid"),
            pricingType,
            basePrice);
    }

    private static void ValidateCategory(Guid? categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("ServiceOffering.CategoryInvalid");
        }
    }
}