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

    // The state machine as data: from each status, where it can go.
    // Rescheduling and setting prices do not change the status, so they are not here.
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Pending] = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.InProgress, AppointmentStatus.NoShow, AppointmentStatus.Cancelled],
        [AppointmentStatus.InProgress] = [AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.Cancelled] = [],
        [AppointmentStatus.NoShow] = [],
    };

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

    /// <summary>Whether the appointment still takes up the agenda of the professional (RN01).</summary>
    public bool OccupiesSchedule =>
        Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.InProgress;

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

    // ---- Status transitions ----

    public bool CanMoveTo(AppointmentStatus to) => AllowedTransitions[Status].Contains(to);

    /// <summary>Confirms a pending appointment. Every service on request must already have its price (RN19).</summary>
    public void Confirm(Guid confirmedBy, DateTime nowUtc)
    {
        ValidateChange(confirmedBy, nowUtc);
        EnsureCanMoveTo(AppointmentStatus.Confirmed);

        // TotalPrice is null exactly while a price is pending.
        var total = TotalPrice ?? throw new DomainException("Appointment.PriceStillPending");

        MoveTo(AppointmentStatus.Confirmed, confirmedBy, nowUtc, null);

        AddDomainEvent(new AppointmentConfirmed(
            Id, CompanyId, BranchId, CustomerId, ProfessionalId, Period.Start, total));
    }

    /// <summary>The service begins.</summary>
    public void Start(Guid startedBy, DateTime nowUtc)
    {
        ValidateChange(startedBy, nowUtc);
        MoveTo(AppointmentStatus.InProgress, startedBy, nowUtc, null);
    }

    /// <summary>The service ends.</summary>
    public void Complete(Guid completedBy, DateTime nowUtc)
    {
        ValidateChange(completedBy, nowUtc);
        MoveTo(AppointmentStatus.Completed, completedBy, nowUtc, null);
    }

    /// <summary>The customer did not show up. Only possible from the start time on.</summary>
    public void MarkAsNoShow(Guid markedBy, DateTime nowUtc)
    {
        ValidateChange(markedBy, nowUtc);
        EnsureCanMoveTo(AppointmentStatus.NoShow);

        if (nowUtc < Period.Start)
        {
            throw new DomainException("Appointment.NoShowBeforeStart");
        }

        MoveTo(AppointmentStatus.NoShow, markedBy, nowUtc, null);
    }

    /// <summary>
    /// The customer cancels. Only until the deadline of the policy; after that, only the company can (RN06).
    /// </summary>
    public void CancelByCustomer(Guid customerId, BookingPolicy policy, DateTime nowUtc, string? reason = null)
    {
        ValidateChange(customerId, nowUtc);

        if (customerId != CustomerId)
        {
            throw new DomainException("Appointment.NotTheCustomer");
        }

        EnsureCanMoveTo(AppointmentStatus.Cancelled);

        // Once the service has started, only the company can cancel (an emergency during the service).
        if (Status == AppointmentStatus.InProgress)
        {
            throw new DomainException("Appointment.CancellationDeadlinePassed");
        }

        // The deadline itself is still allowed.
        if (nowUtc > Period.Start.AddHours(-policy.CancellationDeadlineHours))
        {
            throw new DomainException("Appointment.CancellationDeadlinePassed");
        }

        Cancel(customerId, nowUtc, reason, cancelledByCustomer: true);
    }

    /// <summary>The company cancels. There is no deadline, and it works while the service is in progress too.</summary>
    public void CancelByStaff(Guid userId, DateTime nowUtc, string? reason = null)
    {
        ValidateChange(userId, nowUtc);
        Cancel(userId, nowUtc, reason, cancelledByCustomer: false);
    }

    private void Cancel(Guid cancelledBy, DateTime nowUtc, string? reason, bool cancelledByCustomer)
    {
        var change = MoveTo(AppointmentStatus.Cancelled, cancelledBy, nowUtc, reason);

        AddDomainEvent(new AppointmentCancelled(
            Id, CompanyId, BranchId, CustomerId, ProfessionalId, Period.Start, change.Reason, cancelledByCustomer));
    }

    // ---- Internals ----

    private StatusChange MoveTo(AppointmentStatus to, Guid changedBy, DateTime nowUtc, string? reason)
    {
        EnsureCanMoveTo(to);

        return ChangeStatus(Status, to, nowUtc, changedBy, reason);
    }

    private void EnsureCanMoveTo(AppointmentStatus to)
    {
        if (!CanMoveTo(to))
        {
            throw new DomainException("Appointment.InvalidStatusTransition", Status, to);
        }
    }

    // The only place that changes the status, so the history (RN08) can never be forgotten.
    private StatusChange ChangeStatus(
        AppointmentStatus? from,
        AppointmentStatus to,
        DateTime occurredAtUtc,
        Guid? changedBy,
        string? reason)
    {
        // The history line is built first: if it is invalid, nothing has changed yet.
        var change = StatusChange.Create(from, to, occurredAtUtc, changedBy, reason);

        Status = to;
        _history.Add(change);

        return change;
    }

    // Called whenever the items change.
    private void RecalculateTotal() =>
        TotalPrice = HasPendingPrice
            ? null
            : _items.Aggregate(Money.Zero, (total, item) => item.FinalPrice is { } price ? total.Add(price) : total);

    private static void ValidateChange(Guid changedBy, DateTime nowUtc)
    {
        RequireId(changedBy, "Appointment.ActorRequired");

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("Appointment.TimeMustBeUtc");
        }
    }

    private static void RequireId(Guid id, string errorCode)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(errorCode);
        }
    }
}