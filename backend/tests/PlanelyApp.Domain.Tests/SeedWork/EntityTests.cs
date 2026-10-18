using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SeedWork;

public class EntityTests
{
    [Fact]
    public void New_entity_should_receive_a_version_7_guid()
    {
        var entity = new FakeEntity();

        entity.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Entities_with_same_type_and_id_should_be_equal()
    {
        var id = Guid.CreateVersion7();

        var first = new FakeEntity(id);
        var second = new FakeEntity(id);

        first.ShouldBe(second);
        (first == second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_should_not_be_equal()
    {
        var first = new FakeEntity();
        var second = new FakeEntity();

        first.ShouldNotBe(second);
        (first != second).ShouldBeTrue();
    }

    [Fact]
    public void Entities_with_same_id_but_different_types_should_not_be_equal()
    {
        var id = Guid.CreateVersion7();

        Entity first = new FakeEntity(id);
        Entity second = new OtherEntity(id);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Empty_id_should_be_rejected()
    {
        Should.Throw<ArgumentException>(() => new FakeEntity(Guid.Empty));
    }

    private sealed class FakeEntity : Entity
    {
        public FakeEntity()
        {
        }

        public FakeEntity(Guid id) : base(id)
        {
        }
    }

    private sealed class OtherEntity(Guid id) : Entity(id);
}