using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals;
using PlanelyApp.Application.Organization.Professionals.Get;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class GetProfessionalTests
{
    [Fact]
    public async Task The_professional_should_come_back_with_everything_in_order()
    {
        var professional = ProfessionalData.NewProfessional();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var serviceA = Guid.CreateVersion7();
        var serviceB = Guid.CreateVersion7();

        professional.SetWorkingHours(branchB, ProfessionalData.Day(DayOfWeek.Tuesday, 10, 19).ToDomain());
        professional.SetWorkingHours(branchA, ProfessionalData.Day(DayOfWeek.Monday, 9, 18).ToDomain());
        professional.AddTimeOff(
            DateTimeRange.Create(
                new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 12, 0, 0, 0, DateTimeKind.Utc)),
            "Vacation");
        professional.AddTimeOff(
            DateTimeRange.Create(
                new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 21, 0, 0, 0, DateTimeKind.Utc)),
            null);
        professional.EnableOffering(serviceB, TimeSpan.FromMinutes(45));
        professional.EnableOffering(serviceA);
        var handler = new GetProfessionalHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new GetProfessionalQuery(professional.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(professional.Id);
        dto.Name.ShouldBe("Ana Souza");
        dto.Specialty.ShouldBe("Cabeleireira");
        dto.UserId.ShouldBeNull();
        dto.IsActive.ShouldBeTrue();

        dto.WorkingHours.Select(hours => hours.Schedule.Day)
            .ShouldBe([DayOfWeek.Monday, DayOfWeek.Tuesday], ignoreOrder: false);
        dto.WorkingHours[0].BranchId.ShouldBe(branchA);
        dto.WorkingHours[1].BranchId.ShouldBe(branchB);

        dto.TimeOffs.Select(timeOff => timeOff.StartUtc.Year).ShouldBe([2026, 2027], ignoreOrder: false);
        dto.TimeOffs[0].Reason.ShouldBeNull();
        dto.TimeOffs[1].Reason.ShouldBe("Vacation");

        var expectedOrder = new[] { serviceA, serviceB }.Order().ToList();
        dto.Offerings.Select(offering => offering.ServiceOfferingId).ShouldBe(expectedOrder, ignoreOrder: false);
        dto.Offerings.Single(offering => offering.ServiceOfferingId == serviceB).CustomDurationMinutes.ShouldBe(45);
        dto.Offerings.Single(offering => offering.ServiceOfferingId == serviceA).CustomDurationMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task A_new_professional_should_have_empty_lists()
    {
        var professional = ProfessionalData.NewProfessional();
        var handler = new GetProfessionalHandler(ProfessionalData.RepositoryWith(professional));

        var result = await handler.HandleAsync(
            new GetProfessionalQuery(professional.Id), TestContext.Current.CancellationToken);

        result.Value.WorkingHours.ShouldBeEmpty();
        result.Value.TimeOffs.ShouldBeEmpty();
        result.Value.Offerings.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_professional_should_be_not_found()
    {
        var handler = new GetProfessionalHandler(ProfessionalData.RepositoryWith(null));

        var result = await handler.HandleAsync(
            new GetProfessionalQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }
}