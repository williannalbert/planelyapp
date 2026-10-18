using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.ServiceCategories;

public class ServiceCategoryTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    [Fact]
    public void Create_should_register_the_category_with_a_trimmed_name()
    {
        var category = ServiceCategory.Create(CompanyId, "  Hair ");

        category.Id.Version.ShouldBe(7);
        category.CompanyId.ShouldBe(CompanyId);
        category.Name.ShouldBe("Hair");
    }

    [Fact]
    public void Category_should_be_a_tenant_entity()
    {
        ITenantEntity tenantEntity = ServiceCategory.Create(CompanyId, "Hair");

        tenantEntity.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Empty_company_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ServiceCategory.Create(Guid.Empty, "Hair"))
            .Code.ShouldBe("ServiceCategory.CompanyRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string? name)
    {
        Should.Throw<DomainException>(() => ServiceCategory.Create(CompanyId, name))
            .Code.ShouldBe("ServiceCategory.NameInvalid");
    }

    [Fact]
    public void Name_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ServiceCategory.Create(CompanyId, new string('a', 101)))
            .Code.ShouldBe("ServiceCategory.NameInvalid");
    }

    [Fact]
    public void Rename_should_change_the_name()
    {
        var category = ServiceCategory.Create(CompanyId, "Hair");

        category.Rename("Barber");

        category.Name.ShouldBe("Barber");
    }

    [Fact]
    public void Invalid_rename_should_keep_the_current_name()
    {
        var category = ServiceCategory.Create(CompanyId, "Hair");

        Should.Throw<DomainException>(() => category.Rename(" "));

        category.Name.ShouldBe("Hair");
    }
}