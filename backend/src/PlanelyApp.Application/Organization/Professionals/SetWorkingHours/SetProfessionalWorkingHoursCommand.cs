using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Shared;

namespace PlanelyApp.Application.Organization.Professionals.SetWorkingHours;

/// <summary>Sets the working day of the professional at a branch, replacing the one already set for that branch and weekday.</summary>
public sealed record SetProfessionalWorkingHoursCommand(Guid ProfessionalId, Guid BranchId, DailyScheduleData Schedule)
    : ICommand<Unit>;