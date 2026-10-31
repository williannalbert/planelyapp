using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals;

/// <summary>A service the professional performs. Without a duration of their own, the standard one applies.</summary>
public sealed record OfferingData(Guid ServiceOfferingId, int? CustomDurationMinutes)
{
    internal static OfferingData From(ProfessionalOffering offering) => new(
        offering.ServiceOfferingId,
        offering.CustomDuration is { } duration ? (int)duration.TotalMinutes : null);
}