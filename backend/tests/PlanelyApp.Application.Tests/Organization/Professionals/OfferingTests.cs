using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.DisableOffering;
using PlanelyApp.Application.Organization.Professionals.EnableOffering;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class OfferingTests
{
    private static EnableProfessionalOfferingHandler EnableHandler(Professional? professional, ServiceOffering? service) =>
        new(ProfessionalData.RepositoryWith(professional), ProfessionalData.ServiceRepositoryWith(service));

    [Fact]
    public async Task A_service_should_be_enabled_with_its_standard_duration_and_raise_the_agenda_version()
    {
        var professional = ProfessionalData.NewProfessional();
        var service = ProfessionalData.NewService();

        var result = await EnableHandler(professional, service).HandleAsync(
            new EnableProfessionalOfferingCommand(professional.Id, service.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.CanPerform(service.Id).ShouldBeTrue();
        professional.GetOffering(service.Id)!.CustomDuration.ShouldBeNull();
        professional.ScheduleVersion.ShouldBe(1);
    }

    [Fact]
    public async Task A_service_should_be_enabled_with_a_duration_of_its_own()
    {
        var professional = ProfessionalData.NewProfessional();
        var service = ProfessionalData.NewService();

        await EnableHandler(professional, service).HandleAsync(
            new EnableProfessionalOfferingCommand(professional.Id, service.Id, 45), TestContext.Current.CancellationToken);

        professional.GetOffering(service.Id)!.CustomDuration.ShouldBe(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public async Task Enabling_a_service_again_should_change_its_duration()
    {
        var professional = ProfessionalData.NewProfessional();
        var service = ProfessionalData.NewService();
        var handler = EnableHandler(professional, service);
        var cancellationToken = TestContext.Current.CancellationToken;

        await handler.HandleAsync(new EnableProfessionalOfferingCommand(professional.Id, service.Id, 45), cancellationToken);
        await handler.HandleAsync(new EnableProfessionalOfferingCommand(professional.Id, service.Id, 60), cancellationToken);

        professional.Offerings.ShouldHaveSingleItem().CustomDuration.ShouldBe(TimeSpan.FromMinutes(60));
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task A_duration_of_zero_should_be_refused_by_the_domain()
    {
        var professional = ProfessionalData.NewProfessional();
        var service = ProfessionalData.NewService();

        var exception = await Should.ThrowAsync<DomainException>(() => EnableHandler(professional, service).HandleAsync(
            new EnableProfessionalOfferingCommand(professional.Id, service.Id, 0),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ProfessionalOffering.DurationInvalid");
        professional.Offerings.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task A_missing_professional_should_be_not_found_without_looking_for_the_service()
    {
        var services = ProfessionalData.ServiceRepositoryWith(ProfessionalData.NewService());
        var handler = new EnableProfessionalOfferingHandler(ProfessionalData.RepositoryWith(null), services);

        var result = await handler.HandleAsync(
            new EnableProfessionalOfferingCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
        await services.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_service_should_be_not_found_and_leave_the_agenda_alone()
    {
        var professional = ProfessionalData.NewProfessional();

        var result = await EnableHandler(professional, null).HandleAsync(
            new EnableProfessionalOfferingCommand(professional.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceOffering.NotFound");
        professional.Offerings.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Disabling_a_service_should_remove_it_and_raise_the_agenda_version_again()
    {
        var professional = ProfessionalData.NewProfessional();
        var serviceId = Guid.NewGuid();
        professional.EnableOffering(serviceId);
        var handler = new DisableProfessionalOfferingHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new DisableProfessionalOfferingCommand(professional.Id, serviceId), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.CanPerform(serviceId).ShouldBeFalse();
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Disabling_a_service_that_was_not_enabled_should_change_nothing()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new DisableProfessionalOfferingHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new DisableProfessionalOfferingCommand(professional.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Disabling_a_service_of_a_missing_professional_should_be_not_found()
    {
        var handler = new DisableProfessionalOfferingHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new DisableProfessionalOfferingCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
    }
}