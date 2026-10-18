using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Professionals;

/// <summary>The working day of a professional at one branch, in the branch's local time.</summary>
public sealed class WorkingHours : ValueObject
{
    private WorkingHours(Guid branchId, DailySchedule schedule)
    {
        BranchId = branchId;
        Schedule = schedule;
    }

    public Guid BranchId { get; }

    public DailySchedule Schedule { get; }

    public static WorkingHours Create(Guid branchId, DailySchedule schedule)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainException("WorkingHours.BranchRequired");
        }

        return new WorkingHours(branchId, schedule);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BranchId;
        yield return Schedule;
    }
}