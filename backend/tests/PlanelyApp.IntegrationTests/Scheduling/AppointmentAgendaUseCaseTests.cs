using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Scheduling.Appointments.Cancel;
using PlanelyApp.Application.Scheduling.Appointments.ListByBranch;
using PlanelyApp.Application.Scheduling.Appointments.ListByCustomer;
using PlanelyApp.Application.Scheduling.Appointments.ListByProfessional;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

public class AppointmentAgendaUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task The_staff_should_list_the_day_of_a_branch_with_names_and_services()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var first = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        var second = await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);

        var day = await mysql.QueryAsStaffAsync(
            booking.Company,
            booking.Staff,
            new ListBranchAppointmentsQuery(booking.Centro, BookingData.Day, BookingData.Day),
            cancellationToken);
        var nextDay = await mysql.QueryAsStaffAsync(
            booking.Company,
            booking.Staff,
            new ListBranchAppointmentsQuery(booking.Centro, BookingData.Day.AddDays(1), BookingData.Day.AddDays(1)),
            cancellationToken);

        day.IsSuccess.ShouldBeTrue();
        day.Value.Items.Select(item => item.Id).ToList().ShouldBe([first, second], ignoreOrder: false);
        var item = day.Value.Items[0];
        item.BranchName.ShouldBe("Centro");
        item.ProfessionalName.ShouldBe("Ana Souza");
        item.CustomerId.ShouldBe(booking.Customer);
        item.Services.ShouldBe(["Haircut"], ignoreOrder: false);
        item.StartUtc.ShouldBe(BookingData.At(10));
        item.TotalPrice.ShouldBe(50m);
        item.Status.ShouldBe(AppointmentStatus.Pending);
        nextDay.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_staff_should_list_the_agenda_of_a_professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);

        var result = await mysql.QueryAsStaffAsync(
            booking.Company,
            booking.Staff,
            new ListProfessionalAppointmentsQuery(
                booking.Ana, BookingData.At(0), BookingData.At(0).AddDays(1)),
            cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Staff_of_another_branch_should_not_see_the_branch_and_should_see_nothing_of_the_professional()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        var restricted = StaffData.NewUser(booking.Company);
        restricted.SetBranchAccess(false, [booking.Norte]);
        await mysql.SaveAsync(booking.Company, cancellationToken,
            services => services.GetRequiredService<IUserRepository>().Add(restricted));

        var branch = await mysql.QueryAsStaffAsync(
            booking.Company,
            restricted.Id,
            new ListBranchAppointmentsQuery(booking.Centro, BookingData.Day, BookingData.Day),
            cancellationToken);
        var professional = await mysql.QueryAsStaffAsync(
            booking.Company,
            restricted.Id,
            new ListProfessionalAppointmentsQuery(booking.Ana, BookingData.At(0), BookingData.At(0).AddDays(1)),
            cancellationToken);
        var customer = await mysql.QueryAsStaffAsync(
            booking.Company, restricted.Id, new ListCustomerAppointmentsQuery(booking.Customer), cancellationToken);

        branch.Error.Type.ShouldBe(ErrorType.Forbidden);
        branch.Error.Code.ShouldBe("Branch.AccessDenied");
        professional.Value.TotalCount.ShouldBe(0);
        customer.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_customer_should_list_their_history_and_their_upcoming_without_the_cancelled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var cancelled = await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        var kept = await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);
        await mysql.SendAsStaffAsync(
            booking.Company, booking.Staff, new CancelAppointmentCommand(cancelled), cancellationToken);

        var history = await mysql.QueryAsCustomerAsync(
            booking.Company, booking.Customer, new ListCustomerAppointmentsQuery(booking.Customer), cancellationToken);
        var upcoming = await mysql.QueryAsCustomerAsync(
            booking.Company,
            booking.Customer,
            new ListCustomerAppointmentsQuery(booking.Customer, OnlyUpcoming: true),
            cancellationToken);
        var other = await mysql.QueryAsCustomerAsync(
            booking.Company,
            Guid.CreateVersion7(),
            new ListCustomerAppointmentsQuery(booking.Customer),
            cancellationToken);
        var branch = await mysql.QueryAsCustomerAsync(
            booking.Company,
            booking.Customer,
            new ListBranchAppointmentsQuery(booking.Centro, BookingData.Day, BookingData.Day),
            cancellationToken);

        history.Value.Items.Select(item => item.Id).ToList().ShouldBe([kept, cancelled], ignoreOrder: false);
        upcoming.Value.Items.Select(item => item.Id).ToList().ShouldBe([kept], ignoreOrder: false);
        other.Error.Type.ShouldBe(ErrorType.NotFound);
        other.Error.Code.ShouldBe("Customer.NotFound");
        branch.Error.Type.ShouldBe(ErrorType.Forbidden);
        branch.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    [Fact]
    public async Task The_staff_should_list_the_appointments_of_a_customer_and_not_find_a_customer_that_does_not_exist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(10), cancellationToken);
        await mysql.BookAsync(booking, BookingData.At(14), cancellationToken);

        var result = await mysql.QueryAsStaffAsync(
            booking.Company, booking.Staff, new ListCustomerAppointmentsQuery(booking.Customer), cancellationToken);
        var missing = await mysql.QueryAsStaffAsync(
            booking.Company,
            booking.Staff,
            new ListCustomerAppointmentsQuery(Guid.CreateVersion7()),
            cancellationToken);

        result.Value.TotalCount.ShouldBe(2);
        missing.Error.Type.ShouldBe(ErrorType.NotFound);
        missing.Error.Code.ShouldBe("Customer.NotFound");
    }
}