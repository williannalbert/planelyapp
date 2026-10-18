using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Professionals;

public sealed class Professional : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;
    private const int SpecialtyMaxLength = 100;

    private readonly List<WorkingHours> _workingHours = [];

    private Professional(Guid companyId, string name, string? specialty, Guid? userId)
    {
        CompanyId = companyId;
        Name = name;
        Specialty = specialty;
        UserId = userId;
        IsActive = true;
    }

    public Guid CompanyId { get; }

    public string Name { get; private set; }

    public string? Specialty { get; private set; }

    /// <summary>The staff login of this professional. Null when the professional does not use the system.</summary>
    public Guid? UserId { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Ordered by weekday, then by start time.</summary>
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours.AsReadOnly();

    public static Professional Create(Guid companyId, string? name, string? specialty = null, Guid? userId = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("Professional.CompanyRequired");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("Professional.UserInvalid");
        }

        return new Professional(
            companyId,
            Guard.RequiredText(name, NameMaxLength, "Professional.NameInvalid"),
            Guard.OptionalText(specialty, SpecialtyMaxLength, "Professional.SpecialtyInvalid"),
            userId);
    }

    public void UpdateDetails(string? name, string? specialty)
    {
        var validName = Guard.RequiredText(name, NameMaxLength, "Professional.NameInvalid");
        var validSpecialty = Guard.OptionalText(specialty, SpecialtyMaxLength, "Professional.SpecialtyInvalid");

        Name = validName;
        Specialty = validSpecialty;
    }

    public void LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Professional.UserInvalid");
        }

        UserId = userId;
    }

    public void UnlinkUser() => UserId = null;

    /// <summary>
    /// Sets the working day at a branch, replacing the one already set for that branch and weekday.
    /// The professional cannot be at two branches at the same time (RN10).
    /// </summary>
    public void SetWorkingHours(Guid branchId, DailySchedule schedule)
    {
        var hours = Professionals.WorkingHours.Create(branchId, schedule);

        var conflicts = _workingHours.Any(existing =>
            existing.Schedule.Day == schedule.Day
            && existing.BranchId != branchId
            && existing.Schedule.Range.Overlaps(schedule.Range));

        if (conflicts)
        {
            throw new DomainException("Professional.WorkingHoursConflict");
        }

        _workingHours.RemoveAll(existing =>
            existing.BranchId == branchId && existing.Schedule.Day == schedule.Day);
        _workingHours.Add(hours);
        _workingHours.Sort(CompareWorkingHours);
    }

    /// <summary>Removes the working day at that branch. Does nothing if there was none.</summary>
    public void RemoveWorkingHours(Guid branchId, DayOfWeek day) =>
        _workingHours.RemoveAll(existing => existing.BranchId == branchId && existing.Schedule.Day == day);

    public DailySchedule? GetWorkingHours(Guid branchId, DayOfWeek day) =>
        _workingHours
            .FirstOrDefault(existing => existing.BranchId == branchId && existing.Schedule.Day == day)
            ?.Schedule;

    public bool WorksAt(Guid branchId) => _workingHours.Any(existing => existing.BranchId == branchId);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static int CompareWorkingHours(WorkingHours left, WorkingHours right)
    {
        var byDay = left.Schedule.Day.CompareTo(right.Schedule.Day);
        if (byDay != 0)
        {
            return byDay;
        }

        var byStart = left.Schedule.Range.Start.CompareTo(right.Schedule.Range.Start);
        return byStart != 0 ? byStart : left.BranchId.CompareTo(right.BranchId);
    }
}