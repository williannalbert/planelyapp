namespace PlanelyApp.Application.Scheduling.Appointments.Events;

/// <summary>The type of each message is also its routing key, so renaming one is a change of contract.</summary>
internal static class AppointmentMessageTypes
{
    public const string Created = "appointment.created";
    public const string Confirmed = "appointment.confirmed";
    public const string Cancelled = "appointment.cancelled";
    public const string Rescheduled = "appointment.rescheduled";
    public const string ServicesChanged = "appointment.services_changed";
    public const string ReminderDue = "appointment.reminder_due";
}