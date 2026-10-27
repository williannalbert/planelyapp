using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SeedWork;

public class ValueObjectTests
{
    [Fact]
    public void Value_objects_with_same_components_should_be_equal()
    {
        var first = new Point(1, 2);
        var second = new Point(1, 2);

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Value_objects_with_different_components_should_not_be_equal()
    {
        var first = new Point(1, 2);
        var second = new Point(2, 1);

        first.ShouldNotBe(second);
        (first != second).ShouldBeTrue();
    }

    [Fact]
    public void Value_objects_of_different_types_should_not_be_equal()
    {
        ValueObject first = new Point(1, 2);
        ValueObject second = new OtherPoint(1, 2);

        first.ShouldNotBe(second);
    }

    private sealed class Point(int x, int y) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return x;
            yield return y;
        }
    }

    private sealed class OtherPoint(int x, int y) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return x;
            yield return y;
        }
    }
}