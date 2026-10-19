using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Catalog.Promotions;

/// <summary>
/// A discount on fixed price services, in force during a period, optionally only on some weekdays,
/// in all branches of the company or only in some of them (RN16, RN17, RN20).
/// A per-service promotion discounts each listed service on its own; a bundle discounts the sum
/// of all its services and only applies when all of them are in the same appointment.
/// </summary>
public sealed class Promotion : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 1000;
    private const int BundleMinimumServices = 2;

    private readonly List<Guid> _branchIds = [];
    private readonly List<Guid> _serviceIds = [];
    private readonly List<DayOfWeek> _weekdays = [];

    private Promotion(
        Guid companyId,
        PromotionKind kind,
        string name,
        string? description,
        Discount discount,
        ValidityPeriod validity)
    {
        CompanyId = companyId;
        Kind = kind;
        Name = name;
        Description = description;
        Discount = discount;
        Validity = validity;
        IsActive = true;
    }

    public Guid CompanyId { get; }

    public PromotionKind Kind { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Discount Discount { get; private set; }

    public ValidityPeriod Validity { get; private set; }

    public bool AppliesToAllBranches { get; private set; }

    /// <summary>Empty when the promotion applies to all branches.</summary>
    public IReadOnlyList<Guid> BranchIds => _branchIds.AsReadOnly();

    /// <summary>Always has at least one service (RN17); a bundle has at least two.</summary>
    public IReadOnlyList<Guid> ServiceIds => _serviceIds.AsReadOnly();

    /// <summary>The weekdays the promotion is in force, ordered. Empty means every day.</summary>
    public IReadOnlyList<DayOfWeek> Weekdays => _weekdays.AsReadOnly();

    public bool IsActive { get; private set; }

    public static Promotion CreateForAllBranches(
        Guid companyId,
        string? name,
        Discount discount,
        ValidityPeriod validity,
        IEnumerable<ServiceOffering> services,
        string? description = null) =>
        Create(companyId, PromotionKind.PerService, name, description, discount, validity, services,
            allBranches: true, branchIds: null);

    public static Promotion CreateForBranches(
        Guid companyId,
        string? name,
        Discount discount,
        ValidityPeriod validity,
        IEnumerable<ServiceOffering> services,
        IEnumerable<Guid> branchIds,
        string? description = null) =>
        Create(companyId, PromotionKind.PerService, name, description, discount, validity, services,
            allBranches: false, branchIds);

    public static Promotion CreateBundleForAllBranches(
        Guid companyId,
        string? name,
        Discount discount,
        ValidityPeriod validity,
        IEnumerable<ServiceOffering> services,
        string? description = null) =>
        Create(companyId, PromotionKind.Bundle, name, description, discount, validity, services,
            allBranches: true, branchIds: null);

    public static Promotion CreateBundleForBranches(
        Guid companyId,
        string? name,
        Discount discount,
        ValidityPeriod validity,
        IEnumerable<ServiceOffering> services,
        IEnumerable<Guid> branchIds,
        string? description = null) =>
        Create(companyId, PromotionKind.Bundle, name, description, discount, validity, services,
            allBranches: false, branchIds);

    public void UpdateDetails(string? name, string? description)
    {
        // Validate everything before assigning, so a failure leaves the promotion untouched.
        var validName = Guard.RequiredText(name, NameMaxLength, "Promotion.NameInvalid");
        var validDescription = Guard.OptionalText(description, DescriptionMaxLength, "Promotion.DescriptionInvalid");

        Name = validName;
        Description = validDescription;
    }

    public void ChangeDiscount(Discount discount) => Discount = discount;

    public void ChangeValidity(ValidityPeriod validity) => Validity = validity;

    /// <summary>Restricts the promotion to these weekdays. An empty list means every day.</summary>
    public void SetWeekdays(IEnumerable<DayOfWeek> weekdays)
    {
        var validWeekdays = weekdays.Distinct().Order().ToList();

        if (validWeekdays.Any(day => !Enum.IsDefined(day)))
        {
            throw new DomainException("Promotion.WeekdayInvalid");
        }

        _weekdays.Clear();
        _weekdays.AddRange(validWeekdays);
    }

    /// <summary>Replaces the scope: the promotion now applies only to these branches (RN17).</summary>
    public void ApplyToBranches(IEnumerable<Guid> branchIds)
    {
        var validBranchIds = ValidateBranchIds(branchIds);

        AppliesToAllBranches = false;
        _branchIds.Clear();
        _branchIds.AddRange(validBranchIds);
    }

    public void ApplyToAllBranches()
    {
        AppliesToAllBranches = true;
        _branchIds.Clear();
    }

    /// <summary>
    /// Adds services to the promotion. Only fixed price services of the same company are accepted (RN20, RN07);
    /// if any of them is rejected, none is added. In a bundle, every added service becomes required.
    /// </summary>
    public void IncludeServices(IEnumerable<ServiceOffering> services)
    {
        var validServiceIds = ValidateServices(CompanyId, services);

        foreach (var serviceId in validServiceIds.Where(id => !_serviceIds.Contains(id)))
        {
            _serviceIds.Add(serviceId);
        }
    }

    /// <summary>
    /// Removes a service. Does nothing if it was not in the promotion; the last one cannot be removed (RN17),
    /// and a bundle cannot go below two.
    /// </summary>
    public void RemoveService(Guid serviceOfferingId)
    {
        if (!_serviceIds.Contains(serviceOfferingId))
        {
            return;
        }

        if (Kind == PromotionKind.Bundle && _serviceIds.Count <= BundleMinimumServices)
        {
            throw new DomainException("Promotion.BundleRequiresAtLeastTwoServices");
        }

        if (_serviceIds.Count == 1)
        {
            throw new DomainException("Promotion.ServicesRequired");
        }

        _serviceIds.Remove(serviceOfferingId);
    }

    /// <summary>
    /// Whether this per-service promotion applies to the service in the branch on the given local date of the branch.
    /// The caller converts the appointment instant using the time zone of the branch. Always false for a bundle.
    /// </summary>
    public bool AppliesTo(Guid branchId, Guid serviceOfferingId, DateOnly localDate) =>
        Kind == PromotionKind.PerService
        && IsInForce(branchId, localDate)
        && _serviceIds.Contains(serviceOfferingId);

    /// <summary>
    /// Whether this bundle applies to an appointment that has these services, in the branch, on the given
    /// local date of the branch: every service of the bundle must be present. Always false for a per-service promotion.
    /// </summary>
    public bool AppliesToBundle(Guid branchId, IEnumerable<Guid> presentServiceIds, DateOnly localDate)
    {
        if (Kind != PromotionKind.Bundle || !IsInForce(branchId, localDate))
        {
            return false;
        }

        var present = presentServiceIds.ToHashSet();

        return _serviceIds.All(present.Contains);
    }

    public Money CalculateDiscount(Money price) => Discount.CalculateOn(price);

    /// <summary>Ends the promotion. Appointments already made keep the price they got.</summary>
    public void End() => IsActive = false;

    private bool IsInForce(Guid branchId, DateOnly localDate) =>
        IsActive
        && Validity.Contains(localDate)
        && (_weekdays.Count == 0 || _weekdays.Contains(localDate.DayOfWeek))
        && (AppliesToAllBranches || _branchIds.Contains(branchId));

    private static Promotion Create(
        Guid companyId,
        PromotionKind kind,
        string? name,
        string? description,
        Discount discount,
        ValidityPeriod validity,
        IEnumerable<ServiceOffering> services,
        bool allBranches,
        IEnumerable<Guid>? branchIds)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("Promotion.CompanyRequired");
        }

        var validName = Guard.RequiredText(name, NameMaxLength, "Promotion.NameInvalid");
        var validDescription = Guard.OptionalText(description, DescriptionMaxLength, "Promotion.DescriptionInvalid");
        var validServiceIds = ValidateServices(companyId, services);

        if (validServiceIds.Count == 0)
        {
            throw new DomainException("Promotion.ServicesRequired");
        }

        if (kind == PromotionKind.Bundle && validServiceIds.Count < BundleMinimumServices)
        {
            throw new DomainException("Promotion.BundleRequiresAtLeastTwoServices");
        }

        var validBranchIds = allBranches ? [] : ValidateBranchIds(branchIds);

        var promotion = new Promotion(companyId, kind, validName, validDescription, discount, validity);
        promotion._serviceIds.AddRange(validServiceIds);
        promotion.AppliesToAllBranches = allBranches;
        promotion._branchIds.AddRange(validBranchIds);

        // The event carries a copy: it is a fact about the moment of creation and must not follow later changes.
        promotion.AddDomainEvent(new PromotionCreated(
            promotion.Id,
            companyId,
            allBranches,
            [.. validBranchIds]));

        return promotion;
    }

    private static List<Guid> ValidateServices(Guid companyId, IEnumerable<ServiceOffering> services)
    {
        var serviceIds = new List<Guid>();

        foreach (var service in services)
        {
            if (service.CompanyId != companyId)
            {
                throw new DomainException("Promotion.ServiceFromAnotherCompany");
            }

            if (service.PricingType != PricingType.Fixed)
            {
                throw new DomainException("Promotion.OnRequestServiceNotAllowed");
            }

            if (!serviceIds.Contains(service.Id))
            {
                serviceIds.Add(service.Id);
            }
        }

        return serviceIds;
    }

    private static List<Guid> ValidateBranchIds(IEnumerable<Guid>? branchIds)
    {
        var validBranchIds = branchIds?.Distinct().ToList() ?? [];

        if (validBranchIds.Count == 0)
        {
            throw new DomainException("Promotion.BranchesRequired");
        }

        if (validBranchIds.Contains(Guid.Empty))
        {
            throw new DomainException("Promotion.BranchInvalid");
        }

        return validBranchIds;
    }
}