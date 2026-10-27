using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Catalog.ServiceOfferings;

public class ServiceOfferingTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private static ServiceOffering CreateFixed(
        string? name = "Haircut",
        TimeSpan? duration = null,
        string? description = null,
        Guid? categoryId = null) =>
        ServiceOffering.CreateWithFixedPrice(
            CompanyId,
            name,
            duration ?? TimeSpan.FromMinutes(45),
            Money.From(60m),
            description,
            categoryId);

    private static ServiceOffering CreateOnRequest() =>
        ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3));

    [Fact]
    public void CreateWithFixedPrice_should_register_an_active_fixed_price_service()
    {
        var categoryId = Guid.CreateVersion7();

        var service = CreateFixed(name: "  Haircut ", description: " Wash and cut ", categoryId: categoryId);

        service.Id.Version.ShouldBe(7);
        service.CompanyId.ShouldBe(CompanyId);
        service.Name.ShouldBe("Haircut");
        service.Description.ShouldBe("Wash and cut");
        service.CategoryId.ShouldBe(categoryId);
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(45));
        service.PricingType.ShouldBe(PricingType.Fixed);
        service.BasePrice.ShouldBe(Money.From(60m));
        service.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void CreateOnRequest_should_have_no_base_price()
    {
        var service = CreateOnRequest();

        service.PricingType.ShouldBe(PricingType.OnRequest);
        service.BasePrice.ShouldBeNull();
        service.CategoryId.ShouldBeNull();
        service.Description.ShouldBeNull();
    }

    [Fact]
    public void A_free_fixed_price_service_should_be_accepted()
    {
        var service = ServiceOffering.CreateWithFixedPrice(
            CompanyId, "Free evaluation", TimeSpan.FromMinutes(15), Money.Zero);

        service.BasePrice.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Service_should_be_a_tenant_entity()
    {
        ITenantEntity tenantEntity = CreateFixed();

        tenantEntity.CompanyId.ShouldBe(CompanyId);
    }

    [Fact]
    public void Empty_company_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => ServiceOffering.CreateOnRequest(Guid.Empty, "Tattoo", TimeSpan.FromHours(1)))
            .Code.ShouldBe("ServiceOffering.CompanyRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string? name)
    {
        Should.Throw<DomainException>(() => CreateFixed(name: name))
            .Code.ShouldBe("ServiceOffering.NameInvalid");
    }

    [Fact]
    public void Name_and_description_above_the_limit_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateFixed(name: new string('a', 151)))
            .Code.ShouldBe("ServiceOffering.NameInvalid");
        Should.Throw<DomainException>(() => CreateFixed(description: new string('a', 2001)))
            .Code.ShouldBe("ServiceOffering.DescriptionInvalid");
    }

    [Fact]
    public void Empty_category_id_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateFixed(categoryId: Guid.Empty))
            .Code.ShouldBe("ServiceOffering.CategoryInvalid");
    }

    [Fact]
    public void Invalid_duration_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateFixed(duration: TimeSpan.Zero))
            .Code.ShouldBe("ServiceOffering.DurationInvalid");
        Should.Throw<DomainException>(() => CreateFixed(duration: TimeSpan.FromMinutes(-5)))
            .Code.ShouldBe("ServiceOffering.DurationInvalid");
        Should.Throw<DomainException>(() => CreateFixed(duration: TimeSpan.FromSeconds(90)))
            .Code.ShouldBe("ServiceOffering.DurationInvalid");
        Should.Throw<DomainException>(() => CreateFixed(duration: TimeSpan.FromHours(25)))
            .Code.ShouldBe("ServiceOffering.DurationInvalid");
    }

    [Fact]
    public void SetFixedPrice_on_an_on_request_service_should_make_it_fixed()
    {
        var service = CreateOnRequest();

        service.SetFixedPrice(Money.From(500m));

        service.PricingType.ShouldBe(PricingType.Fixed);
        service.BasePrice.ShouldBe(Money.From(500m));
    }

    [Fact]
    public void SetFixedPrice_on_a_fixed_service_should_replace_the_price()
    {
        var service = CreateFixed();

        service.SetFixedPrice(Money.From(80m));

        service.BasePrice.ShouldBe(Money.From(80m));
    }

    [Fact]
    public void SetOnRequest_should_remove_the_base_price()
    {
        var service = CreateFixed();

        service.SetOnRequest();

        service.PricingType.ShouldBe(PricingType.OnRequest);
        service.BasePrice.ShouldBeNull();
    }

    [Fact]
    public void UpdateDetails_should_change_the_service_data()
    {
        var service = CreateFixed(categoryId: Guid.CreateVersion7());

        service.UpdateDetails("Haircut and beard", " Full service ", TimeSpan.FromMinutes(60), null);

        service.Name.ShouldBe("Haircut and beard");
        service.Description.ShouldBe("Full service");
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(60));
        service.CategoryId.ShouldBeNull();
        service.BasePrice.ShouldBe(Money.From(60m));
    }

    [Fact]
    public void Invalid_update_should_leave_the_service_unchanged()
    {
        var service = CreateFixed();

        Should.Throw<DomainException>(() =>
                service.UpdateDetails("Haircut and beard", "Full service", TimeSpan.Zero, null))
            .Code.ShouldBe("ServiceOffering.DurationInvalid");

        service.Name.ShouldBe("Haircut");
        service.Description.ShouldBeNull();
        service.StandardDuration.ShouldBe(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void Deactivate_and_activate_should_toggle_the_flag()
    {
        var service = CreateFixed();

        service.Deactivate();
        service.IsActive.ShouldBeFalse();

        service.Activate();
        service.IsActive.ShouldBeTrue();
    }
}