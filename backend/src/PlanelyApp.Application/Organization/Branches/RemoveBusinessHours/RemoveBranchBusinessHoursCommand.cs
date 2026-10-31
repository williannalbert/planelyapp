using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Branches.RemoveBusinessHours;

/// <summary>Closes the branch on a weekday. Nothing happens if it was already closed.</summary>
public sealed record RemoveBranchBusinessHoursCommand(Guid BranchId, DayOfWeek Day) : ICommand<Unit>;