using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>
/// A booking of one professional, in one branch, for one customer, with one or more services.
/// Availability (RN01, RN02) and the professional and branch rules (RN04, RN09, RN14)
/// are checked before creating it, by the availability service and the use case.
/// </summary>
public sealed class Appointment : AggregateRoot, ITenantEntity
{
    private const int NotesMaxLength = 1000;

    private readonly List<AppointmentItem> _items = [];
    private readonly List<StatusChange> _history = [];

    private Appointment(
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid professionalId,
        DateTimeRange period,
        string? notes)
    {
        CompanyId = companyId;
        BranchId = branchId;
        CustomerId = customerId;
        ProfessionalId = professionalId;
        Period = period;
        Notes = notes;
    }

    public Guid CompanyId { get; }

    public Guid BranchId { get; }

    public Guid CustomerId { get; }

    public Guid ProfessionalId { get; }

    /// <summary>UTC. Lasts as long as the services plus the interval of the policy (RN05).</summary>
    public DateTimeRange Period { get; private set; }

    public AppointmentStatus Status { get; private set; }

    /// <summary>Sum of the final prices. Null while any service on request has no price yet.</summary>
    public Money? TotalPrice { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyList<AppointmentItem> Items => _items.AsReadOnly();

    public IReadOnlyList<StatusChange> History => _history.AsReadOnly();

    public bool HasPendingPrice => _items.Any(item => item.HasPendingPrice);

    /// <summary>Sum of the durations of the services, without the interval of the policy.</summary>
    public TimeSpan ServicesDuration => new(_items.Sum(item => item.Duration.Ticks));

    public static Appointment Create(
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid professionalId,
        DateTime startUtc,
        IEnumerable<NewAppointmentItem> items,
        BookingPolicy policy,
        DateTime nowUtc,
        Guid createdBy,
        string? notes = null)
    {
        RequireId(companyId, "Appointment.CompanyRequired");
        RequireId(branchId, "Appointment.BranchRequired");
        RequireId(customerId, "Appointment.CustomerRequired");
        RequireId(professionalId, "Appointment.ProfessionalRequired");
        RequireId(createdBy, "Appointment.CreatedByRequired");

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Appointment.TimeMustBeUtc");
        }

        var validNotes = Guard.OptionalText(notes, NotesMaxLength, "Appointment.NotesInvalid");

        var newItems = items.ToList();

        if (newItems.Count == 0)
        {
            throw new DomainException("Appointment.ItemsRequired");
        }

        if (newItems.Any(item => item.Service.CompanyId != companyId))
        {
            throw new DomainException("Appointment.ServiceFromAnotherCompany");
        }

        var appointmentItems = newItems.Select(AppointmentItem.Create).ToList();

        // RN05: the services plus the interval between appointments.
        var servicesDuration = new TimeSpan(appointmentItems.Sum(item => item.Duration.Ticks));
        var interval = TimeSpan.FromMinutes(policy.IntervalBetweenAppointmentsMinutes);
        var period = DateTimeRange.FromDuration(startUtc, servicesDuration + interval);

        // RN03: minimum notice and maximum window. Both limits are allowed values.
        if (startUtc < nowUtc.AddHours(policy.MinimumNoticeHours))
        {
            throw new DomainException("Appointment.MinimumNoticeNotMet");
        }

        if (startUtc > nowUtc.AddDays(policy.MaximumWindowDays))
        {
            throw new DomainException("Appointment.BeyondBookingWindow");
        }

        // A service on request always waits for its price. Otherwise the policy decides.
        var needsConfirmation = policy.RequiresConfirmation || appointmentItems.Any(item => item.IsOnRequest);
        var status = needsConfirmation ? AppointmentStatus.Pending : AppointmentStatus.Confirmed;

        var appointment = new Appointment(companyId, branchId, customerId, professionalId, period, validNotes);
        appointment._items.AddRange(appointmentItems);
        appointment.RecalculateTotal();
        appointment.ChangeStatus(null, status, nowUtc, createdBy, null);

        appointment.AddDomainEvent(new AppointmentCreated(
            appointment.Id,
            companyId,
            branchId,
            customerId,
            professionalId,
            period.Start,
            status));

        return appointment;
    }

    // The only place that changes the status, so the history (RN08) can never be forgotten.
    private void ChangeStatus(
        AppointmentStatus? from,
        AppointmentStatus to,
        DateTime occurredAtUtc,
        Guid? changedBy,
        string? reason)
    {
        Status = to;
        _history.Add(StatusChange.Create(from, to, occurredAtUtc, changedBy, reason));
    }

    // Called whenever the items change.
    private void RecalculateTotal() =>
        TotalPrice = HasPendingPrice
            ? null
            : _items.Aggregate(Money.Zero, (total, item) => item.FinalPrice is { } price ? total.Add(price) : total);

    private static void RequireId(Guid id, string errorCode)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(errorCode);
        }
    }
}