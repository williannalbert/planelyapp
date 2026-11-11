using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Professionals;

namespace PlanelyApp.Application.Organization.Professionals;

/// <summary>The working day of a professional at one branch, in the local time of that branch.</summary>
public sealed record WorkingHoursData(Guid BranchId, DailyScheduleData Schedule)
{
    internal static WorkingHoursData From(WorkingHours hours) =>
        new(hours.BranchId, DailyScheduleData.From(hours.Schedule));
}