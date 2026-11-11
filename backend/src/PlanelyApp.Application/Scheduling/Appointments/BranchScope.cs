namespace PlanelyApp.Application.Scheduling.Appointments;

/// <summary>The branches a member of the staff may see (RN11). <see cref="BranchIds"/> null is every branch.</summary>
internal sealed record BranchScope(IReadOnlyCollection<Guid>? BranchIds)
{
    public bool Allows(Guid branchId) => BranchIds is null || BranchIds.Contains(branchId);
}