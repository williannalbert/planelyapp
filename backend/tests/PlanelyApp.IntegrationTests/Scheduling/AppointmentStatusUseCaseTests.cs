using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Application.Scheduling.Appointments.Complete;
using PlanelyApp.Application.Scheduling.Appointments.Confirm;
using PlanelyApp.Application.Scheduling.Appointments.Create;
using PlanelyApp.Application.Scheduling.Appointments.MarkNoShow;
using PlanelyApp.Application.Scheduling.Appointments.Start;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>The whole way, as a request has it. The appointments are booked by the use case of the sub-phase before.</summary>
public class AppointmentStatusUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_booked_appointment_should_be_confirmed_by_the_staff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken);

        var result = await StaffAsync(booking, new ConfirmAppointmentCommand(id), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var saved = await LoadAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe(AppointmentStatus.Confirmed);
        saved.History.Count.ShouldBe(2);
        var change = saved.History.Single(line => line.To == AppointmentStatus.Confirmed);
        change.From.ShouldBe(AppointmentStatus.Pending);
        change.ChangedBy.ShouldBe(booking.Staff);
    }

    [Fact]
    public async Task A_service_on_request_without_a_price_should_not_be_confirmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await BookingData.CreateServiceAsync(
            mysql, booking.Company, "Tattoo", new PricingData(PricingType.OnRequest), cancellationToken);
        await mysql.SendAsync(
            booking.Company, new EnableProfessionalOfferingCommand(booking.Ana, tattoo), cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken, tattoo);

        var exception = await Should.ThrowAsync<DomainException>(
            () => StaffAsync(booking, new ConfirmAppointmentCommand(id), cancellationToken));

        exception.Code.ShouldBe("Appointment.PriceStillPending");
        (await LoadAsync(booking.Company, id, cancellationToken))!.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task An_appointment_should_be_started_and_completed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await SavePastAppointmentAsync(booking, cancellationToken);

        var started = await StaffAsync(booking, new StartAppointmentCommand(id), cancellationToken);
        var completed = await StaffAsync(booking, new CompleteAppointmentCommand(id), cancellationToken);

        started.IsSuccess.ShouldBeTrue();
        completed.IsSuccess.ShouldBeTrue();
        var saved = await LoadAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe(AppointmentStatus.Completed);
        saved.History.Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_no_show_should_be_possible_only_from_the_start_time_on()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var past = await SavePastAppointmentAsync(booking, cancellationToken);
        var future = await BookAsync(booking, BookingData.At(10), cancellationToken);
        await StaffAsync(booking, new ConfirmAppointmentCommand(future), cancellationToken);

        var earlyAttempt = await Should.ThrowAsync<DomainException>(
            () => StaffAsync(booking, new MarkAppointmentAsNoShowCommand(future), cancellationToken));
        var result = await StaffAsync(booking, new MarkAppointmentAsNoShowCommand(past), cancellationToken);

        earlyAttempt.Code.ShouldBe("Appointment.NoShowBeforeStart");
        result.IsSuccess.ShouldBeTrue();
        (await LoadAsync(booking.Company, past, cancellationToken))!.Status.ShouldBe(AppointmentStatus.NoShow);
        (await LoadAsync(booking.Company, future, cancellationToken))!.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task A_customer_should_cancel_their_own_appointment_and_the_time_should_be_free_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken);

        var busy = await TryBookAsync(booking, BookingData.At(10), cancellationToken);
        var result = await mysql.SendAsCustomerAsync(
            booking.Company, booking.Customer, new CancelAppointmentCommand(id, "Changed my mind"), cancellationToken);
        var free = await TryBookAsync(booking, BookingData.At(10), cancellationToken);

        busy.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        result.IsSuccess.ShouldBeTrue();
        var saved = await LoadAsync(booking.Company, id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe(AppointmentStatus.Cancelled);
        var change = saved.History.Single(line => line.To == AppointmentStatus.Cancelled);
        change.ChangedBy.ShouldBe(booking.Customer);
        change.Reason.ShouldBe("Changed my mind");
        free.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_customer_should_not_find_the_appointment_of_another_customer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken);

        var result = await mysql.SendAsCustomerAsync(
            booking.Company, Guid.CreateVersion7(), new CancelAppointmentCommand(id), cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
        (await LoadAsync(booking.Company, id, cancellationToken))!.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task Staff_should_need_access_to_the_branch_and_to_the_company_of_the_appointment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var other = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken);
        var restricted = StaffData.NewUser(booking.Company);
        restricted.SetBranchAccess(false, [booking.Norte]);
        await mysql.SaveAsync(booking.Company, cancellationToken,
            services => services.GetRequiredService<IUserRepository>().Add(restricted));
        var command = new ConfirmAppointmentCommand(id);

        var withoutTheBranch = await mysql.SendAsStaffAsync(booking.Company, restricted.Id, command, cancellationToken);
        var ofAnotherCompany = await mysql.SendAsStaffAsync(other.Company, other.Staff, command, cancellationToken);

        withoutTheBranch.Error.Type.ShouldBe(ErrorType.Forbidden);
        withoutTheBranch.Error.Code.ShouldBe("Branch.AccessDenied");
        ofAnotherCompany.Error.Type.ShouldBe(ErrorType.NotFound);
        ofAnotherCompany.Error.Code.ShouldBe("Appointment.NotFound");
        (await LoadAsync(booking.Company, id, cancellationToken))!.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public async Task A_customer_should_not_use_the_actions_of_the_staff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var id = await BookAsync(booking, BookingData.At(10), cancellationToken);

        var result = await mysql.SendAsCustomerAsync(
            booking.Company, booking.Customer, new ConfirmAppointmentCommand(id), cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    // ---- Helpers ----

    private Task<Result<Unit>> StaffAsync(Booking booking, ICommand<Unit> command, CancellationToken cancellationToken) =>
        mysql.SendAsStaffAsync(booking.Company, booking.Staff, command, cancellationToken);

    private Task<Result<Guid>> TryBookAsync(
        Booking booking,
        DateTime startUtc,
        CancellationToken cancellationToken,
        Guid? service = null) =>
        mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new CreateAppointmentCommand(
                booking.Centro, booking.Customer, booking.Ana, startUtc, [service ?? booking.Haircut]),
            cancellationToken);

    private async Task<Guid> BookAsync(
        Booking booking,
        DateTime startUtc,
        CancellationToken cancellationToken,
        Guid? service = null) =>
        (await TryBookAsync(booking, startUtc, cancellationToken, service)).Value;

    // An appointment that already began, confirmed from birth. The Domain only compares the start with the time it is
    // given, so it is created "three days ago" for a start "yesterday". The clock of the use cases is the real one.
    private async Task<Guid> SavePastAppointmentAsync(Booking booking, CancellationToken cancellationToken)
    {
        var service = CatalogData.NewFixedService(booking.Company);
        var appointment = Appointment.Create(
            booking.Company,
            booking.Centro,
            booking.Customer,
            booking.Ana,
            SchedulingData.Now.AddDays(-1).AddHours(2),
            [SchedulingData.FixedItem(service)],
            BookingPolicy.Default,
            SchedulingData.Now.AddDays(-3),
            SchedulingData.Actor);

        await mysql.SaveAsync(booking.Company, cancellationToken,
            services => services.GetRequiredService<IAppointmentRepository>().Add(appointment));

        return appointment.Id;
    }

    private async Task<Appointment?> LoadAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().GetByIdAsync(id, cancellationToken);
    }
}