using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals;

/// <summary>A period in which the professional is unavailable. The instants are UTC.</summary>
public sealed record TimeOffData(Guid Id, DateTime StartUtc, DateTime EndUtc, string? Reason)
{
    internal static TimeOffData From(TimeOff timeOff) =>
        new(timeOff.Id, timeOff.Range.Start, timeOff.Range.End, timeOff.Reason);
}