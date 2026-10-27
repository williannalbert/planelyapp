using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class BreakTests
{
    private static TimeOfDayRange Lunch() =>
        TimeOfDayRange.Create(new TimeOnly(12, 0), new TimeOnly(13, 0));

    [Fact]
    public void Should_keep_range_and_trimmed_description()
    {
        var pause = Break.Create(Lunch(), "  Lunch ");

        pause.Range.ShouldBe(Lunch());
        pause.Description.ShouldBe("Lunch");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_description_should_become_null(string? description)
    {
        Break.Create(Lunch(), description).Description.ShouldBeNull();
    }

    [Fact]
    public void Description_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Break.Create(Lunch(), new string('a', 101)))
            .Code.ShouldBe("Break.DescriptionInvalid");
    }

    [Fact]
    public void Breaks_with_same_range_and_description_should_be_equal()
    {
        Break.Create(Lunch(), "Lunch").ShouldBe(Break.Create(Lunch(), "Lunch"));
        Break.Create(Lunch(), "Lunch").ShouldNotBe(Break.Create(Lunch(), "Coffee"));
    }
}