using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.Promotions;
using PlanelyApp.Application.Catalog.Promotions.Create;
using PlanelyApp.Application.Catalog.ServiceOfferings;
using PlanelyApp.Application.Catalog.ServiceOfferings.Deactivate;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Application.Scheduling.Appointments.Create;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using PlanelyApp.IntegrationTests.Catalog;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Scheduling;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repositories, calculator and MySQL.</summary>
public class CreateAppointmentUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task A_booking_should_be_saved_pending_with_its_price_and_a_new_version_of_the_agenda()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var before = await LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        var result = await BookAsync(booking, BookingData.At(10), cancellationToken, notes: "Prefers the window chair");

        result.IsSuccess.ShouldBeTrue();
        var saved = await LoadAppointmentAsync(booking.Company, result.Value, cancellationToken);
        saved.ShouldNotBeNull();
        saved.BranchId.ShouldBe(booking.Centro);
        saved.CustomerId.ShouldBe(booking.Customer);
        saved.ProfessionalId.ShouldBe(booking.Ana);
        saved.Notes.ShouldBe("Prefers the window chair");
        saved.TotalPrice.ShouldBe(Money.From(50m));

        // 30 minutes of service plus 10 of interval, and the company asks for confirmation.
        saved.Period.ShouldBe(DateTimeRange.Create(BookingData.At(10), BookingData.At(10).AddMinutes(40)));
        saved.Status.ShouldBe(AppointmentStatus.Pending);
        saved.Items.ShouldHaveSingleItem().ServiceOfferingId.ShouldBe(booking.Haircut);
        saved.History.ShouldHaveSingleItem().To.ShouldBe(AppointmentStatus.Pending);

        before.ShouldNotBeNull();
        var after = await LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before.ScheduleVersion + 1);
    }

    [Fact]
    public async Task A_service_on_request_should_be_saved_pending_without_a_total()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var tattoo = await BookingData.CreateServiceAsync(
            mysql, booking.Company, "Tattoo", new PricingData(PricingType.OnRequest), cancellationToken);
        await mysql.SendAsync(
            booking.Company, new EnableProfessionalOfferingCommand(booking.Ana, tattoo), cancellationToken);

        var result = await BookAsync(booking, BookingData.At(10), cancellationToken, services: [tattoo]);

        var saved = await LoadAppointmentAsync(booking.Company, result.Value, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Status.ShouldBe(AppointmentStatus.Pending);
        saved.TotalPrice.ShouldBeNull();
        saved.HasPendingPrice.ShouldBeTrue();
        saved.Period.Duration.ShouldBe(TimeSpan.FromMinutes(130)); // 120 plus the interval of 10
    }

    [Fact]
    public async Task A_promotion_should_be_applied_to_the_price_that_is_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        await mysql.SendAsync(
            booking.Company,
            new CreatePromotionCommand(
                PromotionKind.PerService,
                "Week",
                new DiscountData(DiscountType.Percentage, 10m),
                BookingData.Day.AddDays(-1),
                BookingData.Day.AddDays(1),
                [booking.Haircut],
                AppliesToAllBranches: true),
            cancellationToken);

        var result = await BookAsync(booking, BookingData.At(10), cancellationToken);

        var saved = await LoadAppointmentAsync(booking.Company, result.Value, cancellationToken);
        saved.ShouldNotBeNull();
        saved.TotalPrice.ShouldBe(Money.From(45m));
        var item = saved.Items.ShouldHaveSingleItem();
        item.Discount.ShouldBe(Money.From(5m));
        item.PromotionId.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_second_booking_for_the_same_time_should_be_a_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);

        var first = await BookAsync(booking, BookingData.At(10), cancellationToken);
        var second = await BookAsync(booking, BookingData.At(10), cancellationToken);

        first.IsSuccess.ShouldBeTrue();
        second.Error.Type.ShouldBe(ErrorType.Conflict);
        second.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        (await CountAsync(booking, cancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Two_bookings_at_the_same_moment_should_save_only_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var before = await LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);

        // Either the second sees the first and answers with a conflict, or both read the agenda at the same version
        // and the second save fails (RN27). In both cases it must not be saved.
        async Task<bool> TryAsync()
        {
            try
            {
                return (await BookAsync(booking, BookingData.At(10), cancellationToken)).IsSuccess;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(TryAsync(), TryAsync());

        outcomes.Count(won => won).ShouldBe(1);
        (await CountAsync(booking, cancellationToken)).ShouldBe(1);
        var after = await LoadProfessionalAsync(booking.Company, booking.Ana, cancellationToken);
        after!.ScheduleVersion.ShouldBe(before!.ScheduleVersion + 1);
    }

    [Fact]
    public async Task An_appointment_at_another_branch_should_block_the_travel_time_and_not_a_minute_more()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);

        // 13:00 to 13:30 at Norte. The appointment is made with the default policy (no interval), so it ends at 13:30,
        // and the travel time of the company (45 minutes) blocks Ana from 12:15 to 14:15 at Centro.
        await SaveAppointmentAtAsync(booking, booking.Norte, BookingData.At(13), cancellationToken);

        var overlapping = await BookAsync(booking, BookingData.At(14), cancellationToken);
        var justAfter = await BookAsync(booking, BookingData.At(14, 15), cancellationToken);

        overlapping.Error.Type.ShouldBe(ErrorType.Conflict);
        overlapping.Error.Code.ShouldBe("Appointment.TimeNotAvailable");
        justAfter.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task A_customer_should_book_for_themselves_and_not_for_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var command = new CreateAppointmentCommand(
            booking.Centro, booking.Customer, booking.Ana, BookingData.At(10), [booking.Haircut]);

        var other = await mysql.SendAsCustomerAsync(booking.Company, Guid.CreateVersion7(), command, cancellationToken);
        var own = await mysql.SendAsCustomerAsync(booking.Company, booking.Customer, command, cancellationToken);

        other.Error.Type.ShouldBe(ErrorType.Forbidden);
        other.Error.Code.ShouldBe("Appointment.NotTheCustomer");
        own.IsSuccess.ShouldBeTrue();
        (await CountAsync(booking, cancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Staff_should_need_access_to_the_branch_and_to_the_company()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var other = await BookingData.SeedAsync(mysql, cancellationToken);
        var restricted = StaffData.NewUser(booking.Company);
        restricted.SetBranchAccess(false, [booking.Norte]);
        await mysql.SaveAsync(booking.Company, cancellationToken,
            services => services.GetRequiredService<IUserRepository>().Add(restricted));
        var command = new CreateAppointmentCommand(
            booking.Centro, booking.Customer, booking.Ana, BookingData.At(10), [booking.Haircut]);

        var withoutTheBranch = await mysql.SendAsStaffAsync(booking.Company, restricted.Id, command, cancellationToken);
        var ofAnotherCompany = await mysql.SendAsStaffAsync(booking.Company, other.Staff, command, cancellationToken);

        withoutTheBranch.Error.Type.ShouldBe(ErrorType.Forbidden);
        withoutTheBranch.Error.Code.ShouldBe("Branch.AccessDenied");
        ofAnotherCompany.Error.Type.ShouldBe(ErrorType.Forbidden);
        ofAnotherCompany.Error.Code.ShouldBe("Branch.AccessDenied");
        (await CountAsync(booking, cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task What_cannot_be_booked_should_be_refused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);
        var other = await BookingData.SeedAsync(mysql, cancellationToken);
        var inactive = await BookingData.CreateServiceAsync(
            mysql, booking.Company, "Old cut", new PricingData(PricingType.Fixed, 20m), cancellationToken);
        await mysql.SendAsync(booking.Company, new DeactivateServiceOfferingCommand(inactive), cancellationToken);
        var bruno = await BookingData.CreateProfessionalAsync(
            mysql, booking.Company, "Bruno Lima", booking.Centro, null, cancellationToken);
        var nameless = Customer.Register(booking.Company, Email.Create(StaffData.NewEmail()), StaffData.NewPhone());
        await mysql.SaveAsync(booking.Company, cancellationToken,
            services => services.GetRequiredService<ICustomerRepository>().Add(nameless));

        var inactiveService = await SendAsync(booking, booking.Centro, booking.Customer, booking.Ana, [inactive], cancellationToken);
        var notPerformed = await SendAsync(booking, booking.Centro, booking.Customer, bruno, [booking.Haircut], cancellationToken);
        var withoutName = await SendAsync(booking, booking.Centro, nameless.Id, booking.Ana, [booking.Haircut], cancellationToken);
        var branchOfAnother = await SendAsync(booking, other.Centro, booking.Customer, booking.Ana, [booking.Haircut], cancellationToken);
        var customerOfAnother = await SendAsync(booking, booking.Centro, other.Customer, booking.Ana, [booking.Haircut], cancellationToken);
        var professionalOfAnother = await SendAsync(booking, booking.Centro, booking.Customer, other.Ana, [booking.Haircut], cancellationToken);
        var serviceOfAnother = await SendAsync(booking, booking.Centro, booking.Customer, booking.Ana, [other.Haircut], cancellationToken);

        inactiveService.Error.Code.ShouldBe("ServiceOffering.NotAvailableInBranch");
        notPerformed.Error.Code.ShouldBe("Professional.CannotPerformService");
        withoutName.Error.Code.ShouldBe("Customer.NameRequired");
        branchOfAnother.Error.Code.ShouldBe("Branch.NotFound");
        customerOfAnother.Error.Code.ShouldBe("Customer.NotFound");
        professionalOfAnother.Error.Code.ShouldBe("Professional.NotFound");
        serviceOfAnother.Error.Code.ShouldBe("ServiceOffering.NotFound");
        (await CountAsync(booking, cancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task A_start_beyond_the_booking_window_should_be_refused_by_the_Domain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var booking = await BookingData.SeedAsync(mysql, cancellationToken);

        // The window of the company is 90 days.
        var exception = await Should.ThrowAsync<DomainException>(
            () => BookAsync(booking, BookingData.At(10).AddDays(100), cancellationToken));

        exception.Code.ShouldBe("Appointment.BeyondBookingWindow");
        (await CountAsync(booking, cancellationToken)).ShouldBe(0);
    }

    // ---- Helpers ----

    private Task<Result<Guid>> BookAsync(
        Booking booking,
        DateTime startUtc,
        CancellationToken cancellationToken,
        Guid[]? services = null,
        string? notes = null) =>
        mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new CreateAppointmentCommand(
                booking.Centro, booking.Customer, booking.Ana, startUtc, services ?? [booking.Haircut], notes),
            cancellationToken);

    private Task<Result<Guid>> SendAsync(
        Booking booking,
        Guid branchId,
        Guid customerId,
        Guid professionalId,
        Guid[] services,
        CancellationToken cancellationToken) =>
        mysql.SendAsStaffAsync(
            booking.Company,
            booking.Staff,
            new CreateAppointmentCommand(branchId, customerId, professionalId, BookingData.At(10), services),
            cancellationToken);

    // Straight into the repository: it stands for an appointment made earlier, at another branch.
    private async Task SaveAppointmentAtAsync(
        Booking booking,
        Guid branchId,
        DateTime start,
        CancellationToken cancellationToken)
    {
        var customer = StaffData.NewCustomer(booking.Company);
        var service = CatalogData.NewFixedService(booking.Company);
        var appointment = Appointment.Create(
            booking.Company,
            branchId,
            customer.Id,
            booking.Ana,
            start,
            [SchedulingData.FixedItem(service)],
            BookingPolicy.Default,
            SchedulingData.Now,
            SchedulingData.Actor);

        await mysql.SaveAsync(booking.Company, cancellationToken, services =>
        {
            services.GetRequiredService<ICustomerRepository>().Add(customer);
            services.GetRequiredService<IAppointmentRepository>().Add(appointment);
        });
    }

    private async Task<int> CountAsync(Booking booking, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(booking.Company);
        var found = await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().ListOccupyingScheduleAsync(
            [booking.Ana],
            BookingData.At(0).AddDays(-1),
            BookingData.At(0).AddDays(2),
            cancellationToken);

        return found.Count;
    }

    private async Task<Appointment?> LoadAppointmentAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().GetByIdAsync(id, cancellationToken);
    }

    private async Task<Professional?> LoadProfessionalAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IProfessionalRepository>().GetByIdAsync(id, cancellationToken);
    }
}