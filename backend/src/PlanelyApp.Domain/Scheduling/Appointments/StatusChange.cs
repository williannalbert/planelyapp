using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Domain.Scheduling.Appointments;

/// <summary>One line of the status history of an appointment (RN08).</summary>
public sealed class StatusChange : Entity
{
    private const int ReasonMaxLength = 500;

    private StatusChange(
        AppointmentStatus? from,
        AppointmentStatus to,
        DateTime occurredAtUtc,
        Guid? changedBy,
        string? reason)
    {
        From = from;
        To = to;
        OccurredAtUtc = occurredAtUtc;
        ChangedBy = changedBy;
        Reason = reason;
    }

    /// <summary>Null on creation, when there was no previous status.</summary>
    public AppointmentStatus? From { get; }

    public AppointmentStatus To { get; }

    public DateTime OccurredAtUtc { get; }

    /// <summary>The user or the customer who caused the change. Null when the system did.</summary>
    public Guid? ChangedBy { get; }

    public string? Reason { get; }

    // Only the Appointment aggregate writes its history.
    internal static StatusChange Create(
        AppointmentStatus? from,
        AppointmentStatus to,
        DateTime occurredAtUtc,
        Guid? changedBy,
        string? reason) =>
        new(from, to, occurredAtUtc, changedBy, Guard.OptionalText(reason, ReasonMaxLength, "StatusChange.ReasonInvalid"));
}