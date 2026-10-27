using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.Scheduling.Pricing;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Scheduling.Appointments;

public class AppointmentCreationTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid BranchId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid ProfessionalId = Guid.CreateVersion7();
    private static readonly Guid CreatedBy = Guid.CreateVersion7();

    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddDays(1);

    private static BookingPolicy Policy(
        bool requiresConfirmation = false,
        int interval = 0,
        int noticeHours = 2,
        int windowDays = 60) =>
        BookingPolicy.Create(noticeHours, windowDays, 24, interval, 30, requiresConfirmation);

    private static ServiceOffering FixedService(string name = "Haircut", Guid? companyId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, name, TimeSpan.FromMinutes(45), Money.From(80m));

    private static ServiceOffering OnRequestService() =>
        ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3));

    private static NewAppointmentItem FixedItem(ServiceOffering? service = null, int minutes = 45, decimal price = 80m) =>
        new(service ?? FixedService(), TimeSpan.FromMinutes(minutes), PriceQuote.Fixed(Money.From(price)));

    private static NewAppointmentItem OnRequestItem() =>
        new(OnRequestService(), TimeSpan.FromHours(3), PriceQuote.OnRequest());

    private static Appointment CreateAppointment(
        IEnumerable<NewAppointmentItem>? items = null,
        BookingPolicy? policy = null,
        DateTime? startUtc = null,
        DateTime? nowUtc = null,
        Guid? companyId = null,
        Guid? branchId = null,
        Guid? customerId = null,
        Guid? professionalId = null,
        Guid? createdBy = null,
        string? notes = null) =>
        Appointment.Create(
            companyId ?? CompanyId,
            branchId ?? BranchId,
            customerId ?? CustomerId,
            professionalId ?? ProfessionalId,
            startUtc ?? Start,
            items ?? new[] { FixedItem() },
            policy ?? Policy(),
            nowUtc ?? Now,
            createdBy ?? CreatedBy,
            notes);

    // ---- Status at birth ----

    [Fact]
    public void Create_with_fixed_prices_and_no_confirmation_required_should_be_born_confirmed()
    {
        var appointment = CreateAppointment(policy: Policy(requiresConfirmation: false));

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public void Create_when_the_policy_requires_confirmation_should_be_born_pending()
    {
        var appointment = CreateAppointment(policy: Policy(requiresConfirmation: true));

        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    [Fact]
    public void Create_with_a_service_on_request_should_always_be_born_pending_RN19()
    {
        var appointment = CreateAppointment(
            items: new[] { OnRequestItem() },
            policy: Policy(requiresConfirmation: false));

        appointment.Status.ShouldBe(AppointmentStatus.Pending);
    }

    // ---- Data and snapshot ----

    [Fact]
    public void Create_should_keep_the_references_and_the_notes()
    {
        var appointment = CreateAppointment(notes: "  Allergic to latex  ");

        appointment.CompanyId.ShouldBe(CompanyId);
        appointment.BranchId.ShouldBe(BranchId);
        appointment.CustomerId.ShouldBe(CustomerId);
        appointment.ProfessionalId.ShouldBe(ProfessionalId);
        appointment.Notes.ShouldBe("Allergic to latex");
    }

    [Fact]
    public void Create_should_copy_the_service_data_into_the_items()
    {
        var service = FixedService("Haircut");

        var appointment = CreateAppointment(items: new[] { FixedItem(service, minutes: 30, price: 80m) });

        var item = appointment.Items.Single();
        item.Id.ShouldNotBe(Guid.Empty);
        item.ServiceOfferingId.ShouldBe(service.Id);
        item.ServiceName.ShouldBe("Haircut");
        item.Duration.ShouldBe(TimeSpan.FromMinutes(30));
        item.IsOnRequest.ShouldBeFalse();
        item.ListPrice.ShouldBe(Money.From(80m));
        item.Discount.ShouldBe(Money.Zero);
        item.FinalPrice.ShouldBe(Money.From(80m));
        item.PromotionId.ShouldBeNull();
        item.HasPendingPrice.ShouldBeFalse();
    }

    [Fact]
    public void Create_should_copy_the_discount_and_the_promotion_into_the_item()
    {
        var promotionId = Guid.CreateVersion7();
        var quote = PriceQuote.Fixed(Money.From(100m), Money.From(20m), promotionId);
        var item = new NewAppointmentItem(FixedService(), TimeSpan.FromMinutes(45), quote);

        var appointment = CreateAppointment(items: new[] { item });

        var stored = appointment.Items.Single();
        stored.ListPrice.ShouldBe(Money.From(100m));
        stored.Discount.ShouldBe(Money.From(20m));
        stored.FinalPrice.ShouldBe(Money.From(80m));
        stored.PromotionId.ShouldBe(promotionId);
    }

    [Fact]
    public void The_items_should_not_follow_later_changes_to_the_service()
    {
        var service = FixedService("Haircut");
        var appointment = CreateAppointment(items: new[] { FixedItem(service) });

        service.UpdateDetails("Premium haircut", null, TimeSpan.FromMinutes(90), null);
        service.SetFixedPrice(Money.From(200m));

        var item = appointment.Items.Single();
        item.ServiceName.ShouldBe("Haircut");
        item.Duration.ShouldBe(TimeSpan.FromMinutes(45));
        item.FinalPrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void A_service_on_request_should_be_stored_without_prices()
    {
        var appointment = CreateAppointment(items: new[] { OnRequestItem() });

        var item = appointment.Items.Single();
        item.IsOnRequest.ShouldBeTrue();
        item.ListPrice.ShouldBeNull();
        item.FinalPrice.ShouldBeNull();
        item.HasPendingPrice.ShouldBeTrue();
        appointment.HasPendingPrice.ShouldBeTrue();
    }

    [Fact]
    public void Create_should_keep_the_items_in_the_given_order()
    {
        var haircut = FixedService("Haircut");
        var beard = FixedService("Beard");

        var appointment = CreateAppointment(items: new[] { FixedItem(haircut), FixedItem(beard) });

        appointment.Items.Select(item => item.ServiceName).ShouldBe(["Haircut", "Beard"]);
    }

    // ---- Duration (RN05) and total ----

    [Fact]
    public void The_period_should_last_as_long_as_the_sum_of_the_services_RN05()
    {
        var appointment = CreateAppointment(items: new[] { FixedItem(minutes: 45), FixedItem(minutes: 30) });

        appointment.ServicesDuration.ShouldBe(TimeSpan.FromMinutes(75));
        appointment.Period.Start.ShouldBe(Start);
        appointment.Period.End.ShouldBe(Start.AddMinutes(75));
    }

    [Fact]
    public void The_period_should_include_the_interval_between_appointments_RN05()
    {
        var appointment = CreateAppointment(
            items: new[] { FixedItem(minutes: 45), FixedItem(minutes: 30) },
            policy: Policy(interval: 15));

        appointment.ServicesDuration.ShouldBe(TimeSpan.FromMinutes(75));
        appointment.Period.End.ShouldBe(Start.AddMinutes(90));
    }

    [Fact]
    public void The_total_should_be_the_sum_of_the_final_prices()
    {
        var withDiscount = new NewAppointmentItem(
            FixedService("Haircut"),
            TimeSpan.FromMinutes(45),
            PriceQuote.Fixed(Money.From(100m), Money.From(20m), Guid.CreateVersion7()));

        var appointment = CreateAppointment(items: new[] { withDiscount, FixedItem(price: 60m) });

        appointment.TotalPrice.ShouldBe(Money.From(140m));
    }

    [Fact]
    public void The_total_should_be_null_while_any_price_is_pending()
    {
        var appointment = CreateAppointment(items: new[] { FixedItem(price: 60m), OnRequestItem() });

        appointment.TotalPrice.ShouldBeNull();
        appointment.HasPendingPrice.ShouldBeTrue();
    }

    // ---- History (RN08) and event ----

    [Fact]
    public void Create_should_register_the_first_line_of_the_history_RN08()
    {
        var appointment = CreateAppointment(policy: Policy(requiresConfirmation: true));

        var change = appointment.History.Single();
        change.From.ShouldBeNull();
        change.To.ShouldBe(AppointmentStatus.Pending);
        change.OccurredAtUtc.ShouldBe(Now);
        change.ChangedBy.ShouldBe(CreatedBy);
        change.Reason.ShouldBeNull();
    }

    [Fact]
    public void Create_should_record_the_AppointmentCreated_event()
    {
        var appointment = CreateAppointment(policy: Policy(requiresConfirmation: false));

        var created = appointment.DomainEvents.OfType<AppointmentCreated>().Single();
        created.AppointmentId.ShouldBe(appointment.Id);
        created.CompanyId.ShouldBe(CompanyId);
        created.BranchId.ShouldBe(BranchId);
        created.CustomerId.ShouldBe(CustomerId);
        created.ProfessionalId.ShouldBe(ProfessionalId);
        created.StartUtc.ShouldBe(Start);
        created.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.DomainEvents.Count.ShouldBe(1);
    }

    // ---- Required data ----

    [Fact]
    public void Create_without_the_references_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateAppointment(companyId: Guid.Empty))
            .Code.ShouldBe("Appointment.CompanyRequired");
        Should.Throw<DomainException>(() => CreateAppointment(branchId: Guid.Empty))
            .Code.ShouldBe("Appointment.BranchRequired");
        Should.Throw<DomainException>(() => CreateAppointment(customerId: Guid.Empty))
            .Code.ShouldBe("Appointment.CustomerRequired");
        Should.Throw<DomainException>(() => CreateAppointment(professionalId: Guid.Empty))
            .Code.ShouldBe("Appointment.ProfessionalRequired");
        Should.Throw<DomainException>(() => CreateAppointment(createdBy: Guid.Empty))
            .Code.ShouldBe("Appointment.CreatedByRequired");
    }

    [Fact]
    public void Create_without_items_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateAppointment(items: Array.Empty<NewAppointmentItem>()))
            .Code.ShouldBe("Appointment.ItemsRequired");
    }

    [Fact]
    public void Create_with_a_service_of_another_company_should_be_rejected()
    {
        var foreign = FixedService(companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => CreateAppointment(items: new[] { FixedItem(foreign) }))
            .Code.ShouldBe("Appointment.ServiceFromAnotherCompany");
    }

    [Fact]
    public void Create_with_a_quote_that_does_not_match_the_service_should_be_rejected()
    {
        var fixedServiceWithOnRequestQuote = new NewAppointmentItem(
            FixedService(), TimeSpan.FromMinutes(45), PriceQuote.OnRequest());
        var onRequestServiceWithFixedQuote = new NewAppointmentItem(
            OnRequestService(), TimeSpan.FromHours(3), PriceQuote.Fixed(Money.From(80m)));

        Should.Throw<DomainException>(() => CreateAppointment(items: new[] { fixedServiceWithOnRequestQuote }))
            .Code.ShouldBe("Appointment.PriceDoesNotMatchService");
        Should.Throw<DomainException>(() => CreateAppointment(items: new[] { onRequestServiceWithFixedQuote }))
            .Code.ShouldBe("Appointment.PriceDoesNotMatchService");
    }

    [Fact]
    public void Create_with_an_invalid_item_duration_should_be_rejected()
    {
        foreach (var duration in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(90), TimeSpan.FromHours(25) })
        {
            var item = new NewAppointmentItem(FixedService(), duration, PriceQuote.Fixed(Money.From(80m)));

            Should.Throw<DomainException>(() => CreateAppointment(items: new[] { item }))
                .Code.ShouldBe("Appointment.ItemDurationInvalid");
        }
    }

    [Fact]
    public void Create_with_notes_that_are_too_long_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateAppointment(notes: new string('a', 1001)))
            .Code.ShouldBe("Appointment.NotesInvalid");
    }

    // ---- Time ----

    [Fact]
    public void Create_with_a_start_that_is_not_UTC_should_be_rejected()
    {
        var unspecified = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => CreateAppointment(startUtc: unspecified))
            .Code.ShouldBe("DateTimeRange.TimeMustBeUtc");
    }

    [Fact]
    public void Create_with_a_current_time_that_is_not_UTC_should_be_rejected()
    {
        var unspecified = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Unspecified);

        Should.Throw<DomainException>(() => CreateAppointment(nowUtc: unspecified))
            .Code.ShouldBe("Appointment.TimeMustBeUtc");
    }

    [Fact]
    public void Create_exactly_at_the_minimum_notice_should_be_accepted_RN03()
    {
        var appointment = CreateAppointment(startUtc: Now.AddHours(2));

        appointment.Period.Start.ShouldBe(Now.AddHours(2));
    }

    [Fact]
    public void Create_with_less_than_the_minimum_notice_should_be_rejected_RN03()
    {
        Should.Throw<DomainException>(() => CreateAppointment(startUtc: Now.AddHours(2).AddMinutes(-1)))
            .Code.ShouldBe("Appointment.MinimumNoticeNotMet");
    }

    [Fact]
    public void Create_in_the_past_should_be_rejected_even_without_minimum_notice()
    {
        Should.Throw<DomainException>(() => CreateAppointment(
                startUtc: Now.AddMinutes(-1),
                policy: Policy(noticeHours: 0)))
            .Code.ShouldBe("Appointment.MinimumNoticeNotMet");
    }

    [Fact]
    public void Create_exactly_at_the_end_of_the_window_should_be_accepted_RN03()
    {
        var appointment = CreateAppointment(startUtc: Now.AddDays(60));

        appointment.Period.Start.ShouldBe(Now.AddDays(60));
    }

    [Fact]
    public void Create_beyond_the_booking_window_should_be_rejected_RN03()
    {
        Should.Throw<DomainException>(() => CreateAppointment(startUtc: Now.AddDays(60).AddMinutes(1)))
            .Code.ShouldBe("Appointment.BeyondBookingWindow");
    }
}