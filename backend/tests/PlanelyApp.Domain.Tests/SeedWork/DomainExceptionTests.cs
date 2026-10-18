using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SeedWork;

public class DomainExceptionTests
{
    [Fact]
    public void Should_expose_code_and_arguments()
    {
        var exception = new DomainException("Appointment.SlotUnavailable", 3);

        exception.Code.ShouldBe("Appointment.SlotUnavailable");
        exception.Message.ShouldBe("Appointment.SlotUnavailable");
        exception.Arguments.Count.ShouldBe(1);
        exception.Arguments[0].ShouldBe(3);
    }

    [Fact]
    public void Blank_code_should_be_rejected()
    {
        Should.Throw<ArgumentException>(() => new DomainException(" "));
    }
}