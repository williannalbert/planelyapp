namespace PlanelyApp.Domain.Scheduling.Appointments;

public enum AppointmentStatus
{
    /// <summary>Waiting for confirmation or for the price of a service on request (RN19).</summary>
    Pending,

    Confirmed,

    InProgress,

    Completed,

    Cancelled,

    NoShow,
}