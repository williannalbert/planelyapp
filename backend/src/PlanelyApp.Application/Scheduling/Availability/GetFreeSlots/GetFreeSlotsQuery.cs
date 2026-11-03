using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Scheduling.Availability.GetFreeSlots;

/// <summary>
/// The times the services can start on a local day of the branch. Without <see cref="ProfessionalId"/>, every
/// professional of the branch who performs all the services; with it, only that one. Times are multiples of
/// <see cref="StepMinutes"/> on the clock of the branch. The same service may come more than once.
/// </summary>
public sealed record GetFreeSlotsQuery(
    Guid BranchId,
    DateOnly Date,
    IReadOnlyList<Guid> ServiceIds,
    Guid? ProfessionalId = null,
    int StepMinutes = GetFreeSlotsQuery.DefaultStepMinutes) : IQuery<FreeSlotsDto>
{
    public const int DefaultStepMinutes = 15;
}