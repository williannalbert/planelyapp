using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

// Shared building blocks for the appointment tests.
internal static class AppointmentFixtures
{
    public static readonly Guid CompanyId = Guid.CreateVersion7();
    public static readonly Guid BranchId = Guid.CreateVersion7();
    public static readonly Guid CustomerId = Guid.CreateVersion7();
    public static readonly Guid ProfessionalId = Guid.CreateVersion7();
    public static readonly Guid CreatedBy = Guid.CreateVersion7();
    public static readonly Guid StaffId = Guid.CreateVersion7();

    public static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime StartsAt = Now.AddDays(2);
    public static readonly DateTime JustAfterStart = StartsAt.AddMinutes(5);

    public static BookingPolicy Policy(
        bool requiresConfirmation = false,
        int cancellationDeadlineHours = 24,
        int interval = 0) =>
        BookingPolicy.Create(2, 60, cancellationDeadlineHours, interval, 30, requiresConfirmation);

    public static NewAppointmentItem FixedItem(int minutes = 45, decimal price = 80m) =>
        new(
            ServiceOffering.CreateWithFixedPrice(CompanyId, "Haircut", TimeSpan.FromMinutes(45), Money.From(price)),
            TimeSpan.FromMinutes(minutes),
            PriceQuote.Fixed(Money.From(price)));

    public static NewAppointmentItem OnRequestItem(int hours = 3) =>
        new(
            ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(hours)),
            TimeSpan.FromHours(hours),
            PriceQuote.OnRequest());

    // Born pending by default (the policy requires confirmation).
    public static Appointment Create(
        IEnumerable<NewAppointmentItem>? items = null,
        bool requiresConfirmation = true,
        int interval = 0) =>
        Appointment.Create(
            CompanyId,
            BranchId,
            CustomerId,
            ProfessionalId,
            StartsAt,
            items ?? new[] { FixedItem() },
            Policy(requiresConfirmation, interval: interval),
            Now,
            CreatedBy);

    // Goes through the real transitions until the appointment reaches the status.
    public static Appointment InStatus(AppointmentStatus status)
    {
        var appointment = Create();

        switch (status)
        {
            case AppointmentStatus.Pending:
                break;
            case AppointmentStatus.Confirmed:
                appointment.Confirm(StaffId, Now);
                break;
            case AppointmentStatus.InProgress:
                appointment.Confirm(StaffId, Now);
                appointment.Start(StaffId, StartsAt);
                break;
            case AppointmentStatus.Completed:
                appointment.Confirm(StaffId, Now);
                appointment.Start(StaffId, StartsAt);
                appointment.Complete(StaffId, StartsAt.AddMinutes(45));
                break;
            case AppointmentStatus.Cancelled:
                appointment.CancelByStaff(StaffId, Now);
                break;
            case AppointmentStatus.NoShow:
                appointment.Confirm(StaffId, Now);
                appointment.MarkAsNoShow(StaffId, JustAfterStart);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        appointment.Status.ShouldBe(status);

        return appointment;
    }
}