using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Organization.Professionals;

/// <summary>A service the professional performs, optionally with a duration of their own.</summary>
public sealed class ProfessionalOffering : ValueObject
{
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    private ProfessionalOffering(Guid serviceOfferingId, TimeSpan? customDuration)
    {
        ServiceOfferingId = serviceOfferingId;
        CustomDuration = customDuration;
    }

    public Guid ServiceOfferingId { get; }

    /// <summary>When null, the standard duration of the service applies.</summary>
    public TimeSpan? CustomDuration { get; }

    public static ProfessionalOffering Create(Guid serviceOfferingId, TimeSpan? customDuration = null)
    {
        if (serviceOfferingId == Guid.Empty)
        {
            throw new DomainException("ProfessionalOffering.ServiceRequired");
        }

        if (customDuration is { } duration
            && (duration <= TimeSpan.Zero
                || duration > MaximumDuration
                || duration.Ticks % TimeSpan.TicksPerMinute != 0))
        {
            throw new DomainException("ProfessionalOffering.DurationInvalid");
        }

        return new ProfessionalOffering(serviceOfferingId, customDuration);
    }

    public TimeSpan ResolveDuration(TimeSpan standardDuration) => CustomDuration ?? standardDuration;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ServiceOfferingId;
        yield return CustomDuration;
    }
}