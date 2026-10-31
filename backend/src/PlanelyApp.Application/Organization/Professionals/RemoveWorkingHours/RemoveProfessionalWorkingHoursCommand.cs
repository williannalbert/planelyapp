using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Organization.Professionals.RemoveWorkingHours;

/// <summary>Removes the working day at a branch. Nothing happens if there was none.</summary>
public sealed record RemoveProfessionalWorkingHoursCommand(Guid ProfessionalId, Guid BranchId, DayOfWeek Day)
    : ICommand<Unit>;