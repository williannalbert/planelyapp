using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Branches.SetBusinessHours;

/// <summary>Opens a weekday of the branch, or replaces the hours and breaks it already had.</summary>
public sealed record SetBranchBusinessHoursCommand(Guid BranchId, DailyScheduleData Schedule) : ICommand<Unit>;