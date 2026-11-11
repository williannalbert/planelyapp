using PlanelyApp.Application.Abstractions.Results;
using Shouldly;
using UserData = PlanelyApp.Application.Tests.Organization.Users.UserData;

namespace PlanelyApp.Application.Tests.Scheduling;

public class AppointmentAccessTests
{
    private readonly AppointmentScenario _scenario = new();

    [Fact]
    public async Task Staff_with_access_to_the_branch_should_load_the_appointment()
    {
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForStaffAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(appointment);
    }

    [Fact]
    public async Task An_appointment_that_does_not_exist_should_be_not_found()
    {
        var result = await _scenario.Access(null).LoadForStaffAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task Staff_without_access_to_the_branch_should_be_forbidden()
    {
        _scenario.Staff.SetBranchAccess(false, [Guid.NewGuid()]);
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForStaffAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task A_user_who_is_not_of_the_company_should_be_forbidden()
    {
        _scenario.Users = UserData.RepositoryWith(null);
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForStaffAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }

    [Fact]
    public async Task A_customer_should_not_load_for_the_staff()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForStaffAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Appointment.StaffOnly");
    }

    [Fact]
    public async Task A_customer_should_load_their_own_appointment()
    {
        _scenario.ActAsCustomer();
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForActorAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(appointment);
    }

    [Fact]
    public async Task The_appointment_of_another_customer_should_look_like_it_does_not_exist()
    {
        _scenario.ActAsCustomer(Guid.NewGuid());
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForActorAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Appointment.NotFound");
    }

    [Fact]
    public async Task Staff_with_access_should_load_any_appointment_of_the_branch()
    {
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForActorAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Staff_without_access_should_not_load_for_the_actor_either()
    {
        _scenario.Staff.SetBranchAccess(false, [Guid.NewGuid()]);
        var appointment = _scenario.Confirmed();

        var result = await _scenario.Access(appointment).LoadForActorAsync(
            appointment.Id, TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Branch.AccessDenied");
    }
}