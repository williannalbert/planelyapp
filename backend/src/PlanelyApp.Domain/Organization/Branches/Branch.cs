using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Branches;

public sealed class Branch : AggregateRoot, ITenantEntity
{
    private const int NameMaxLength = 100;

    private readonly List<DailySchedule> _businessHours = [];
    private readonly List<BranchOfferingOverride> _offeringOverrides = [];

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

    /// <summary>Only the services where this branch differs from the company default.</summary>
    public IReadOnlyList<BranchOfferingOverride> OfferingOverrides => _offeringOverrides.AsReadOnly();

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

    // ---- Business hours ----

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

    // ---- Services: how this branch differs from the company default ----

    /// <summary>
    /// Gives the service a price of its own in this branch. Only fixed price services can have one (RN20),
    /// and the service must belong to the same company (RN07).
    /// </summary>
    public void SetCustomPrice(ServiceOffering service, Money price)
    {
        if (service.CompanyId != CompanyId)
        {
            throw new DomainException("Branch.ServiceFromAnotherCompany");
        }

        if (service.PricingType != PricingType.Fixed)
        {
            throw new DomainException("Branch.CustomPriceNotAllowed");
        }

        ChangeOverride(service.Id, current => current.WithCustomPrice(price));
    }

    /// <summary>Goes back to the base price of the service. Does nothing if there was no custom price.</summary>
    public void ClearCustomPrice(Guid serviceOfferingId) =>
        ChangeOverride(serviceOfferingId, current => current.WithCustomPrice(null));

    /// <summary>The branch stops offering the service (RN14).</summary>
    public void MakeOfferingUnavailable(Guid serviceOfferingId) =>
        ChangeOverride(serviceOfferingId, current => current.WithAvailability(false));

    /// <summary>The branch offers the service again. Does nothing if it was already offered.</summary>
    public void MakeOfferingAvailable(Guid serviceOfferingId) =>
        ChangeOverride(serviceOfferingId, current => current.WithAvailability(true));

    /// <summary>Whether the branch offers the service. Without an override, it does (RN14).</summary>
    public bool Offers(Guid serviceOfferingId) =>
        _offeringOverrides.FirstOrDefault(existing => existing.ServiceOfferingId == serviceOfferingId)?.IsAvailable
        ?? true;

    /// <summary>The price of its own that this branch gave the service, or null to use the base price.</summary>
    public Money? GetCustomPrice(Guid serviceOfferingId) =>
        _offeringOverrides.FirstOrDefault(existing => existing.ServiceOfferingId == serviceOfferingId)?.CustomPrice;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    // Keeps the invariant that only real deviations from the default are stored.
    private void ChangeOverride(
        Guid serviceOfferingId,
        Func<BranchOfferingOverride, BranchOfferingOverride> change)
    {
        if (serviceOfferingId == Guid.Empty)
        {
            throw new DomainException("Branch.ServiceRequired");
        }

        var index = _offeringOverrides.FindIndex(existing => existing.ServiceOfferingId == serviceOfferingId);
        var current = index >= 0
            ? _offeringOverrides[index]
            : BranchOfferingOverride.CreateDefault(serviceOfferingId);

        var updated = change(current);

        if (index >= 0)
        {
            _offeringOverrides.RemoveAt(index);
        }

        if (!updated.IsDefault)
        {
            _offeringOverrides.Add(updated);
        }
    }

    private static string ValidateTimeZone(string? timeZoneId)
    {
        var trimmed = timeZoneId?.Trim();

        if (string.IsNullOrEmpty(trimmed) || !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _))
        {
            throw new DomainException("Branch.TimeZoneInvalid");
        }

        return trimmed;
    }

    /// <summary>The calendar date at the branch at the given instant (in the branch time zone, not UTC).</summary>
    public DateOnly GetLocalDate(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Branch.InstantMustBeUtc");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instantUtc, timeZone));
    }

    /// <summary>
    /// The UTC instant of a wall-clock time at this branch. A time that does not exist because the clocks
    /// jumped forward is rejected; a time that happens twice when the clocks go back is read as the later one
    /// (standard time).
    /// </summary>
    /// <summary>
    /// The UTC instant of a wall-clock time at this branch. A time the clocks skipped (they jumped forward)
    /// becomes the instant of the jump; a time that happens twice (they went back) is read as the later one,
    /// in standard time.
    /// </summary>
    public DateTime ToUtc(DateOnly localDate, TimeOnly localTime)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var local = localDate.ToDateTime(localTime, DateTimeKind.Unspecified);

        return timeZone.IsInvalidTime(local)
            ? GetTransitionInstant(timeZone, local)
            : TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    // The clocks skipped this local time, so it never happened. The nearest real instant is the moment of the
    // jump, found by bisection: the UTC offset of the zone changes exactly there.
    private static DateTime GetTransitionInstant(TimeZoneInfo timeZone, DateTime skippedLocal)
    {
        var offsetBefore = timeZone.GetUtcOffset(DateTime.SpecifyKind(skippedLocal.AddDays(-1), DateTimeKind.Utc));

        // Read with the old offset, the skipped time lands after the jump; one day earlier it is before it.
        var after = DateTime.SpecifyKind(skippedLocal - offsetBefore, DateTimeKind.Utc);
        var before = after.AddDays(-1);

        var low = 0;                                        // minutes after `before` still with the old offset
        var high = (int)TimeSpan.FromDays(1).TotalMinutes;  // minutes after `before` with the new offset

        while (high - low > 1)
        {
            var middle = (low + high) / 2;

            if (timeZone.GetUtcOffset(before.AddMinutes(middle)) == offsetBefore)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return before.AddMinutes(high);
    }
}