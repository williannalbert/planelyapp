using PlanelyApp.Application.Abstractions.Results;
using Shouldly;

namespace PlanelyApp.Application.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Each_factory_should_set_its_type()
    {
        Error.Validation("A.B").Type.ShouldBe(ErrorType.Validation);
        Error.NotFound("A.B").Type.ShouldBe(ErrorType.NotFound);
        Error.Conflict("A.B").Type.ShouldBe(ErrorType.Conflict);
        Error.Forbidden("A.B").Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void The_code_and_the_arguments_should_be_kept()
    {
        var error = Error.NotFound("Appointment.NotFound", Guid.Empty);

        error.Code.ShouldBe("Appointment.NotFound");
        error.Arguments.ShouldHaveSingleItem().ShouldBe(Guid.Empty);
    }

    [Fact]
    public void An_error_without_arguments_should_have_an_empty_list()
    {
        Error.Conflict("A.B").Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void A_blank_code_should_be_refused()
    {
        Should.Throw<ArgumentException>(() => Error.Forbidden(" "));
    }
}