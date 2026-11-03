using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Application.Scheduling.Appointments.SetItemPrice;

/// <summary>
/// Sets the price agreed for a service on request and, if it is different from the standard, its real duration in
/// minutes. Only the staff, and only while the appointment is pending (RN19).
/// </summary>
public sealed record SetAppointmentItemPriceCommand(
    Guid AppointmentId,
    Guid ItemId,
    decimal Price,
    int? DurationMinutes = null) : ICommand<Unit>;