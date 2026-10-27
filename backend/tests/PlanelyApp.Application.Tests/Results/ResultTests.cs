using PlanelyApp.Application.Abstractions.Results;
using Shouldly;

namespace PlanelyApp.Application.Tests.Results;

public class ResultTests
{
    [Fact]
    public void A_success_should_hold_its_value_and_have_no_error()
    {
        var result = Result<string>.Success("ok");

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe("ok");
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void A_failure_should_hold_its_error_and_have_no_value()
    {
        var error = Error.NotFound("Appointment.NotFound");

        var result = Result<string>.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeSameAs(error);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void A_failure_without_error_should_be_refused()
    {
        Should.Throw<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void A_value_should_convert_to_a_successful_result()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void An_error_should_convert_to_a_failed_result()
    {
        Result<int> result = Error.Conflict("Appointment.SlotUnavailable");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Appointment.SlotUnavailable");
    }

    [Fact]
    public void A_result_without_a_value_should_be_a_success_of_unit()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(Unit.Value);
    }
}