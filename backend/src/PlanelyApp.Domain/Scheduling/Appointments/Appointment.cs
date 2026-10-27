using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>
/// A booking of one professional, in one branch, for one customer, with one or more services.
/// Availability (RN01, RN02) and the professional and branch rules (RN04, RN09, RN14)
/// are checked before creating or moving it, by the availability service and the use case.
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

    #pragma warning disable CS8618
    private Appointment()
    {
    }
    #pragma warning restore CS8618

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

        EnsureBundlesAreComplete(appointmentItems);

        // RN05: the services plus the interval between appointments.
        var servicesDuration = new TimeSpan(appointmentItems.Sum(item => item.Duration.Ticks));
        var interval = TimeSpan.FromMinutes(policy.IntervalBetweenAppointmentsMinutes);
        var period = DateTimeRange.FromDuration(startUtc, servicesDuration + interval);

        EnsureWithinBookingWindow(startUtc, nowUtc, policy);

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
        EnsureIsTheCustomer(customerId);
        EnsureCanMoveTo(AppointmentStatus.Cancelled);

        // Once the service has started, only the company can cancel (an emergency during the service).
        if (Status == AppointmentStatus.InProgress)
        {
            throw new DomainException("Appointment.CancellationDeadlinePassed");
        }

        EnsureWithinCustomerDeadline(policy, nowUtc);

        Cancel(customerId, nowUtc, reason, cancelledByCustomer: true);
    }

    /// <summary>The company cancels. There is no deadline, and it works while the service is in progress too.</summary>
    public void CancelByStaff(Guid userId, DateTime nowUtc, string? reason = null)
    {
        ValidateChange(userId, nowUtc);
        Cancel(userId, nowUtc, reason, cancelledByCustomer: false);
    }

    // ---- Rescheduling (RN03, RN06) ----

    /// <summary>
    /// The customer moves the appointment. Only until the deadline of the policy, counted from the current
    /// start; after that, only the company can (RN06). The new start respects notice and window (RN03).
    /// </summary>
    public void RescheduleByCustomer(Guid customerId, DateTime newStartUtc, BookingPolicy policy, DateTime nowUtc)
    {
        ValidateChange(customerId, nowUtc);
        EnsureIsTheCustomer(customerId);
        EnsureEditable();
        EnsureWithinCustomerDeadline(policy, nowUtc);

        Reschedule(customerId, newStartUtc, policy, nowUtc, rescheduledByCustomer: true);
    }

    /// <summary>The company moves the appointment. There is no deadline, but notice and window still apply (RN03).</summary>
    public void RescheduleByStaff(Guid userId, DateTime newStartUtc, BookingPolicy policy, DateTime nowUtc)
    {
        ValidateChange(userId, nowUtc);
        EnsureEditable();

        Reschedule(userId, newStartUtc, policy, nowUtc, rescheduledByCustomer: false);
    }

    // ---- Services on request (RN19) ----

    /// <summary>
    /// Sets the price agreed for a service on request, and optionally its real duration.
    /// Only while the appointment is pending. When the duration changes, so does the period:
    /// the caller must check the availability again before saving (RN19).
    /// </summary>
    public void SetItemPrice(Guid itemId, Money price, TimeSpan? newDuration = null)
    {
        if (Status != AppointmentStatus.Pending)
        {
            throw new DomainException("Appointment.PriceCanOnlyBeSetWhilePending", Status);
        }

        var item = _items.FirstOrDefault(existing => existing.Id == itemId)
            ?? throw new DomainException("Appointment.ItemNotFound");

        // The price of a fixed price service comes from the price calculator, not from here.
        if (!item.IsOnRequest)
        {
            throw new DomainException("Appointment.ItemHasFixedPrice");
        }

        var interval = CurrentInterval();

        // Throws before changing anything if the duration is invalid.
        item.SetPrice(price, newDuration);

        RecalculateTotal();
        ResizePeriod(interval);
    }

    // ---- Changing the services (RN06) ----
    // When the duration grows, the caller must check the availability again before saving (RN01, RN02).

    /// <summary>The customer adds a service. Only until the deadline of the policy (RN06).</summary>
    public void AddItemByCustomer(Guid customerId, NewAppointmentItem newItem, BookingPolicy policy, DateTime nowUtc)
    {
        ValidateChange(customerId, nowUtc);
        EnsureIsTheCustomer(customerId);
        EnsureEditable();
        EnsureWithinCustomerDeadline(policy, nowUtc);

        AddItem(newItem, customerId, changedByCustomer: true);
    }

    /// <summary>The company adds a service. There is no deadline.</summary>
    public void AddItemByStaff(Guid userId, NewAppointmentItem newItem)
    {
        RequireId(userId, "Appointment.ActorRequired");
        EnsureEditable();

        AddItem(newItem, userId, changedByCustomer: false);
    }

    /// <summary>The customer removes a service. Only until the deadline of the policy (RN06).</summary>
    public void RemoveItemByCustomer(Guid customerId, Guid itemId, BookingPolicy policy, DateTime nowUtc)
    {
        ValidateChange(customerId, nowUtc);
        EnsureIsTheCustomer(customerId);
        EnsureEditable();
        EnsureWithinCustomerDeadline(policy, nowUtc);

        RemoveItem(itemId, customerId, changedByCustomer: true);
    }

    /// <summary>The company removes a service. There is no deadline.</summary>
    public void RemoveItemByStaff(Guid userId, Guid itemId)
    {
        RequireId(userId, "Appointment.ActorRequired");
        EnsureEditable();

        RemoveItem(itemId, userId, changedByCustomer: false);
    }

    // ---- Internals ----

    private void Cancel(Guid cancelledBy, DateTime nowUtc, string? reason, bool cancelledByCustomer)
    {
        var change = MoveTo(AppointmentStatus.Cancelled, cancelledBy, nowUtc, reason);

        AddDomainEvent(new AppointmentCancelled(
            Id, CompanyId, BranchId, CustomerId, ProfessionalId, Period.Start, change.Reason, cancelledByCustomer));
    }

    private void Reschedule(
        Guid rescheduledBy,
        DateTime newStartUtc,
        BookingPolicy policy,
        DateTime nowUtc,
        bool rescheduledByCustomer)
    {
        // Same length, new start. Throws if the new start is not UTC.
        var newPeriod = DateTimeRange.FromDuration(newStartUtc, Period.Duration);

        // Moving to the same time is not a change: no event.
        if (newPeriod == Period)
        {
            return;
        }

        EnsureWithinBookingWindow(newStartUtc, nowUtc, policy);

        var previousStart = Period.Start;
        Period = newPeriod;

        AddDomainEvent(new AppointmentRescheduled(
            Id,
            CompanyId,
            BranchId,
            CustomerId,
            ProfessionalId,
            previousStart,
            newStartUtc,
            rescheduledBy,
            rescheduledByCustomer));
    }

    private void AddItem(NewAppointmentItem newItem, Guid changedBy, bool changedByCustomer)
    {
        if (newItem.Price.IsFromBundle)
        {
            throw new DomainException("Appointment.BundleItemCannotBeAdded");
        }

        if (newItem.Service.CompanyId != CompanyId)
        {
            throw new DomainException("Appointment.ServiceFromAnotherCompany");
        }

        // Validates the duration and that the quote matches the service, before anything changes.
        var item = AppointmentItem.Create(newItem);

        // A confirmed appointment must have every price known (RN19).
        if (item.IsOnRequest && Status != AppointmentStatus.Pending)
        {
            throw new DomainException("Appointment.OnRequestItemRequiresPending", Status);
        }

        var interval = CurrentInterval();
        _items.Add(item);

        AfterItemsChanged(interval, changedBy, changedByCustomer);
    }

    private void RemoveItem(Guid itemId, Guid changedBy, bool changedByCustomer)
    {
        var item = _items.FirstOrDefault(existing => existing.Id == itemId)
            ?? throw new DomainException("Appointment.ItemNotFound");

        // To end the appointment, cancel it.
        if (_items.Count == 1)
        {
            throw new DomainException("Appointment.ItemsRequired");
        }

        var interval = CurrentInterval();
        _items.Remove(item);
        DropBundleDiscountsOf(item);

        AfterItemsChanged(interval, changedBy, changedByCustomer);
    }

    // A discount from a bundle depends on the other services of the bundle, so a bundle needs at least two items.
    private static void EnsureBundlesAreComplete(IEnumerable<AppointmentItem> items)
    {
        var hasIncompleteBundle = items
            .Where(item => item.IsFromBundle)
            .GroupBy(item => item.PromotionId)
            .Any(bundle => bundle.Count() < 2);

        if (hasIncompleteBundle)
        {
            throw new DomainException("Appointment.BundleIncomplete");
        }
    }

    // A bundle only has its discount while all its services are in the appointment:
    // when one is removed, the others go back to their list price.
    private void DropBundleDiscountsOf(AppointmentItem removed)
    {
        if (!removed.IsFromBundle)
        {
            return;
        }

        var siblings = _items
            .Where(item => item.IsFromBundle && item.PromotionId == removed.PromotionId)
            .ToList();

        foreach (var sibling in siblings)
        {
            sibling.DropBundleDiscount();
        }
    }

    private void AfterItemsChanged(TimeSpan interval, Guid changedBy, bool changedByCustomer)
    {
        RecalculateTotal();
        ResizePeriod(interval);

        AddDomainEvent(new AppointmentServicesChanged(
            Id,
            CompanyId,
            BranchId,
            CustomerId,
            ProfessionalId,
            Period.Start,
            Period.End,
            TotalPrice,
            changedBy,
            changedByCustomer));
    }

    // The interval of the policy is whatever was added at creation: the period minus the services.
    private TimeSpan CurrentInterval() => Period.Duration - ServicesDuration;

    // Same start, same interval, new length of the services.
    private void ResizePeriod(TimeSpan interval) =>
        Period = DateTimeRange.FromDuration(Period.Start, ServicesDuration + interval);

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

    // The appointment can still be changed (moved, priced, have services added or removed).
    private void EnsureEditable()
    {
        if (Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
        {
            throw new DomainException("Appointment.NotEditable", Status);
        }
    }

    private void EnsureIsTheCustomer(Guid customerId)
    {
        if (customerId != CustomerId)
        {
            throw new DomainException("Appointment.NotTheCustomer");
        }
    }

    // RN06: the customer can only cancel or move the appointment until the deadline, counted from its
    // current start. The deadline itself is still allowed.
    private void EnsureWithinCustomerDeadline(BookingPolicy policy, DateTime nowUtc)
    {
        if (nowUtc > Period.Start.AddHours(-policy.CancellationDeadlineHours))
        {
            throw new DomainException("Appointment.CancellationDeadlinePassed");
        }
    }

    // RN03: minimum notice and maximum window. Both limits are allowed values.
    private static void EnsureWithinBookingWindow(DateTime startUtc, DateTime nowUtc, BookingPolicy policy)
    {
        if (startUtc < nowUtc.AddHours(policy.MinimumNoticeHours))
        {
            throw new DomainException("Appointment.MinimumNoticeNotMet");
        }

        if (startUtc > nowUtc.AddDays(policy.MaximumWindowDays))
        {
            throw new DomainException("Appointment.BeyondBookingWindow");
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