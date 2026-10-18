using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Branches;

public sealed class Branch : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;

    private readonly List<DailySchedule> _businessHours = [];

    private Branch(Guid companyId, string name, Address address, Phone phone, string timeZoneId)
    {
        CompanyId = companyId;
        Name = name;
        Address = address;
        Phone = phone;
        TimeZoneId = timeZoneId;
        IsActive = true;
    }

    public Guid CompanyId { get; }

    public string Name { get; private set; }

    public Address Address { get; private set; }

    public Phone Phone { get; private set; }

    /// <summary>IANA time zone of the branch (e.g. "America/Sao_Paulo"). Business hours are in this zone.</summary>
    public string TimeZoneId { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>One entry per open weekday, ordered by day. A weekday without an entry is closed.</summary>
    public IReadOnlyList<DailySchedule> BusinessHours => _businessHours.AsReadOnly();

    public static Branch Create(
        Guid companyId,
        string? name,
        Address address,
        Phone phone,
        string? timeZoneId)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("Branch.CompanyRequired");
        }

        return new Branch(
            companyId,
            Guard.RequiredText(name, NameMaxLength, "Branch.NameInvalid"),
            address,
            phone,
            ValidateTimeZone(timeZoneId));
    }

    public void UpdateDetails(string? name, Address address, Phone phone, string? timeZoneId)
    {
        // Validate everything before assigning, so a failure leaves the branch untouched.
        var validName = Guard.RequiredText(name, NameMaxLength, "Branch.NameInvalid");
        var validTimeZoneId = ValidateTimeZone(timeZoneId);

        Name = validName;
        Address = address;
        Phone = phone;
        TimeZoneId = validTimeZoneId;
    }

    /// <summary>Opens (or replaces) the weekday of the given schedule.</summary>
    public void SetBusinessHours(DailySchedule schedule)
    {
        _businessHours.RemoveAll(existing => existing.Day == schedule.Day);
        _businessHours.Add(schedule);
        _businessHours.Sort((left, right) => left.Day.CompareTo(right.Day));
    }

    /// <summary>Closes the branch on the given weekday. Does nothing if it was already closed.</summary>
    public void RemoveBusinessHours(DayOfWeek day) =>
        _businessHours.RemoveAll(existing => existing.Day == day);

    public DailySchedule? GetBusinessHours(DayOfWeek day) =>
        _businessHours.FirstOrDefault(existing => existing.Day == day);

    public bool IsOpenAt(DayOfWeek day, TimeOnly time) =>
        GetBusinessHours(day)?.IsOpenAt(time) ?? false;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string ValidateTimeZone(string? timeZoneId)
    {
        var trimmed = timeZoneId?.Trim();

        if (string.IsNullOrEmpty(trimmed) || !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _))
        {
            throw new DomainException("Branch.TimeZoneInvalid");
        }

        return trimmed;
    }
}