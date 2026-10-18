using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Professionals;

public sealed class Professional : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;
    private const int SpecialtyMaxLength = 100;

    private readonly List<WorkingHours> _workingHours = [];
    private readonly List<TimeOff> _timeOffs = [];
    private readonly List<ProfessionalOffering> _offerings = [];

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

    /// <summary>
    /// Concurrency token of the professional's agenda (RN27). It goes up whenever something that
    /// affects availability changes, so two simultaneous writes cannot both succeed.
    /// </summary>
    public int ScheduleVersion { get; private set; }

    /// <summary>Ordered by weekday, then by start time.</summary>
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours.AsReadOnly();

    /// <summary>Ordered by start.</summary>
    public IReadOnlyList<TimeOff> TimeOffs => _timeOffs.AsReadOnly();

    public IReadOnlyList<ProfessionalOffering> Offerings => _offerings.AsReadOnly();

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

    // ---- Working hours ----

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
        IncrementScheduleVersion();
    }

    /// <summary>Removes the working day at that branch. Does nothing if there was none.</summary>
    public void RemoveWorkingHours(Guid branchId, DayOfWeek day)
    {
        var removed = _workingHours.RemoveAll(existing =>
            existing.BranchId == branchId && existing.Schedule.Day == day);

        if (removed > 0)
        {
            IncrementScheduleVersion();
        }
    }

    public DailySchedule? GetWorkingHours(Guid branchId, DayOfWeek day) =>
        _workingHours
            .FirstOrDefault(existing => existing.BranchId == branchId && existing.Schedule.Day == day)
            ?.Schedule;

    public bool WorksAt(Guid branchId) => _workingHours.Any(existing => existing.BranchId == branchId);

    // ---- Time off ----

    public TimeOff AddTimeOff(DateTimeRange range, string? reason = null)
    {
        var timeOff = TimeOff.Create(range, reason);

        _timeOffs.Add(timeOff);
        _timeOffs.Sort(CompareTimeOffs);
        IncrementScheduleVersion();

        return timeOff;
    }

    public void RemoveTimeOff(Guid timeOffId)
    {
        if (_timeOffs.RemoveAll(timeOff => timeOff.Id == timeOffId) == 0)
        {
            throw new DomainException("Professional.TimeOffNotFound");
        }

        IncrementScheduleVersion();
    }

    public bool HasTimeOffDuring(DateTimeRange range) => _timeOffs.Any(timeOff => timeOff.Range.Overlaps(range));

    // ---- Services the professional performs ----

    /// <summary>Enables a service (or changes its custom duration if already enabled).</summary>
    public void EnableOffering(Guid serviceOfferingId, TimeSpan? customDuration = null)
    {
        var offering = ProfessionalOffering.Create(serviceOfferingId, customDuration);

        _offerings.RemoveAll(existing => existing.ServiceOfferingId == serviceOfferingId);
        _offerings.Add(offering);
        IncrementScheduleVersion();
    }

    /// <summary>Stops the professional from performing the service. Does nothing if it was not enabled.</summary>
    public void DisableOffering(Guid serviceOfferingId)
    {
        var removed = _offerings.RemoveAll(existing => existing.ServiceOfferingId == serviceOfferingId);

        if (removed > 0)
        {
            IncrementScheduleVersion();
        }
    }

    public bool CanPerform(Guid serviceOfferingId) =>
        _offerings.Any(existing => existing.ServiceOfferingId == serviceOfferingId);

    public ProfessionalOffering? GetOffering(Guid serviceOfferingId) =>
        _offerings.FirstOrDefault(existing => existing.ServiceOfferingId == serviceOfferingId);

    // ---- Agenda version ----

    /// <summary>Called by anything that changes the agenda, such as booking or rescheduling an appointment.</summary>
    public void IncrementScheduleVersion() => ScheduleVersion++;

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

    private static int CompareTimeOffs(TimeOff left, TimeOff right)
    {
        var byStart = left.Range.Start.CompareTo(right.Range.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byEnd = left.Range.End.CompareTo(right.Range.End);
        return byEnd != 0 ? byEnd : left.Id.CompareTo(right.Id);
    }
}