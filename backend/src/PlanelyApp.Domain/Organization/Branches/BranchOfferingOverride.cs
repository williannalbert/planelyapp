using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Branches;

/// <summary>
/// How a branch departs from the company default for one service: its own price
/// and/or not offering the service at all. Only created through the Branch.
/// </summary>
public sealed class BranchOfferingOverride : ValueObject
{
    private BranchOfferingOverride(Guid serviceOfferingId, Money? customPrice, bool isAvailable)
    {
        ServiceOfferingId = serviceOfferingId;
        CustomPrice = customPrice;
        IsAvailable = isAvailable;
    }

    public Guid ServiceOfferingId { get; }

    /// <summary>When null, the base price of the service applies (RN13).</summary>
    public Money? CustomPrice { get; }

    /// <summary>False when the branch does not offer the service (RN14).</summary>
    public bool IsAvailable { get; }

    /// <summary>True when it says nothing different from the default, so it must not be stored.</summary>
    internal bool IsDefault => CustomPrice is null && IsAvailable;

    internal static BranchOfferingOverride CreateDefault(Guid serviceOfferingId) =>
        new(serviceOfferingId, null, true);

    internal BranchOfferingOverride WithCustomPrice(Money? customPrice) =>
        new(ServiceOfferingId, customPrice, IsAvailable);

    internal BranchOfferingOverride WithAvailability(bool isAvailable) =>
        new(ServiceOfferingId, CustomPrice, isAvailable);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ServiceOfferingId;
        yield return CustomPrice;
        yield return IsAvailable;
    }
}