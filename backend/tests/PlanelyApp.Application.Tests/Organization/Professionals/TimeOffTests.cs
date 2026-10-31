using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Professionals.AddTimeOff;
using PlanelyApp.Application.Organization.Professionals.RemoveTimeOff;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Professionals;

public class TimeOffTests
{
    private static readonly DateTime Start = new(2027, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2027, 1, 12, 12, 0, 0, DateTimeKind.Utc);

    private static AddProfessionalTimeOffHandler AddHandler(Professional? professional) =>
        new(ProfessionalData.RepositoryWith(professional));

    private static RemoveProfessionalTimeOffHandler RemoveHandler(Professional? professional) =>
        new(ProfessionalData.RepositoryWith(professional));

    [Fact]
    public async Task A_time_off_should_be_added_and_raise_the_agenda_version()
    {
        var professional = ProfessionalData.NewProfessional();

        var result = await AddHandler(professional).HandleAsync(
            new AddProfessionalTimeOffCommand(professional.Id, Start, End, "Vacation"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var timeOff = professional.TimeOffs.ShouldHaveSingleItem();
        result.Value.ShouldBe(timeOff.Id);
        timeOff.Range.Start.ShouldBe(Start);
        timeOff.Range.End.ShouldBe(End);
        timeOff.Reason.ShouldBe("Vacation");
        professional.ScheduleVersion.ShouldBe(1);
    }

    [Fact]
    public async Task An_instant_that_is_not_utc_should_be_refused_by_the_domain()
    {
        var professional = ProfessionalData.NewProfessional();
        var local = new DateTime(2027, 1, 10, 12, 0, 0, DateTimeKind.Unspecified);

        var exception = await Should.ThrowAsync<DomainException>(() => AddHandler(professional).HandleAsync(
            new AddProfessionalTimeOffCommand(professional.Id, local, End), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("DateTimeRange.TimeMustBeUtc");
        professional.TimeOffs.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task An_end_before_the_start_should_be_refused_by_the_domain()
    {
        var professional = ProfessionalData.NewProfessional();

        var exception = await Should.ThrowAsync<DomainException>(() => AddHandler(professional).HandleAsync(
            new AddProfessionalTimeOffCommand(professional.Id, End, Start), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("DateTimeRange.EndMustBeAfterStart");
        professional.TimeOffs.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_reason_above_the_limit_should_be_refused_and_leave_the_agenda_alone()
    {
        var professional = ProfessionalData.NewProfessional();

        var exception = await Should.ThrowAsync<DomainException>(() => AddHandler(professional).HandleAsync(
            new AddProfessionalTimeOffCommand(professional.Id, Start, End, new string('x', 201)),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("TimeOff.ReasonInvalid");
        professional.TimeOffs.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task A_time_off_for_a_missing_professional_should_be_not_found()
    {
        var result = await AddHandler(null).HandleAsync(
            new AddProfessionalTimeOffCommand(Guid.NewGuid(), Start, End), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.NotFound");
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new AddProfessionalTimeOffValidator(), new AddProfessionalTimeOffCommand(Guid.NewGuid(), Start, End));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dates_that_were_not_sent_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new AddProfessionalTimeOffValidator(), new AddProfessionalTimeOffCommand(Guid.NewGuid(), default, default));

        problems.ShouldBe(["StartUtc:Validation.Required", "EndUtc:Validation.Required"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_time_off_should_be_removed_and_raise_the_agenda_version_again()
    {
        var professional = ProfessionalData.NewProfessional();
        var timeOff = professional.AddTimeOff(DateTimeRange.Create(Start, End));

        var result = await RemoveHandler(professional).HandleAsync(
            new RemoveProfessionalTimeOffCommand(professional.Id, timeOff.Id), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        professional.TimeOffs.ShouldBeEmpty();
        professional.ScheduleVersion.ShouldBe(2);
    }

    [Fact]
    public async Task An_unknown_time_off_should_be_not_found_and_change_nothing()
    {
        var professional = ProfessionalData.NewProfessional();

        var result = await RemoveHandler(professional).HandleAsync(
            new RemoveProfessionalTimeOffCommand(professional.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Professional.TimeOffNotFound");
        professional.ScheduleVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Removing_a_time_off_of_a_missing_professional_should_be_not_found()
    {
        var result = await RemoveHandler(null).HandleAsync(
            new RemoveProfessionalTimeOffCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Professional.NotFound");
    }
}