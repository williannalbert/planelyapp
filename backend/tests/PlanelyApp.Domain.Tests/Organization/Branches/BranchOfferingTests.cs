using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Branches;

public class BranchOfferingTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    private static Branch CreateBranch() =>
        Branch.Create(
            CompanyId,
            "Centro",
            Address.Create("Rua das Flores", "123", null, "Centro", "São Paulo", "SP", "01310-100"),
            Phone.Create("+551133334444"),
            "America/Sao_Paulo");

    private static ServiceOffering FixedService(Guid? companyId = null, string name = "Haircut") =>
        ServiceOffering.CreateWithFixedPrice(
            companyId ?? CompanyId, name, TimeSpan.FromMinutes(45), Money.From(60m));

    private static ServiceOffering OnRequestService() =>
        ServiceOffering.CreateOnRequest(CompanyId, "Custom tattoo", TimeSpan.FromHours(3));

    [Fact]
    public void Without_overrides_the_branch_should_offer_every_service_at_the_base_price()
    {
        var branch = CreateBranch();
        var service = FixedService();

        branch.Offers(service.Id).ShouldBeTrue();
        branch.GetCustomPrice(service.Id).ShouldBeNull();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void SetCustomPrice_should_give_the_service_a_price_of_its_own()
    {
        var branch = CreateBranch();
        var service = FixedService();

        branch.SetCustomPrice(service, Money.From(80m));

        branch.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));
        branch.Offers(service.Id).ShouldBeTrue();
        branch.OfferingOverrides.Count.ShouldBe(1);
    }

    [Fact]
    public void SetCustomPrice_again_should_replace_the_price_without_duplicating()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.SetCustomPrice(service, Money.From(80m));

        branch.SetCustomPrice(service, Money.From(90m));

        branch.GetCustomPrice(service.Id).ShouldBe(Money.From(90m));
        branch.OfferingOverrides.Count.ShouldBe(1);
    }

    [Fact]
    public void SetCustomPrice_for_an_on_request_service_should_be_rejected()
    {
        var branch = CreateBranch();
        var service = OnRequestService();

        Should.Throw<DomainException>(() => branch.SetCustomPrice(service, Money.From(500m)))
            .Code.ShouldBe("Branch.CustomPriceNotAllowed");

        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void SetCustomPrice_for_a_service_of_another_company_should_be_rejected()
    {
        var branch = CreateBranch();
        var foreignService = FixedService(companyId: Guid.CreateVersion7());

        Should.Throw<DomainException>(() => branch.SetCustomPrice(foreignService, Money.From(80m)))
            .Code.ShouldBe("Branch.ServiceFromAnotherCompany");

        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void ClearCustomPrice_should_go_back_to_the_base_price_and_drop_the_override()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.SetCustomPrice(service, Money.From(80m));

        branch.ClearCustomPrice(service.Id);

        branch.GetCustomPrice(service.Id).ShouldBeNull();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void ClearCustomPrice_should_keep_the_override_when_the_service_is_unavailable()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.SetCustomPrice(service, Money.From(80m));
        branch.MakeOfferingUnavailable(service.Id);

        branch.ClearCustomPrice(service.Id);

        branch.GetCustomPrice(service.Id).ShouldBeNull();
        branch.Offers(service.Id).ShouldBeFalse();
        branch.OfferingOverrides.Count.ShouldBe(1);
    }

    [Fact]
    public void ClearCustomPrice_without_an_override_should_do_nothing()
    {
        var branch = CreateBranch();

        branch.ClearCustomPrice(Guid.CreateVersion7());

        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void MakeOfferingUnavailable_should_stop_the_branch_from_offering_the_service()
    {
        var branch = CreateBranch();
        var service = FixedService();

        branch.MakeOfferingUnavailable(service.Id);

        branch.Offers(service.Id).ShouldBeFalse();
        branch.OfferingOverrides.Count.ShouldBe(1);
    }

    [Fact]
    public void MakeOfferingUnavailable_should_work_for_on_request_services_too()
    {
        var branch = CreateBranch();
        var service = OnRequestService();

        branch.MakeOfferingUnavailable(service.Id);

        branch.Offers(service.Id).ShouldBeFalse();
    }

    [Fact]
    public void MakeOfferingUnavailable_should_keep_the_custom_price()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.SetCustomPrice(service, Money.From(80m));

        branch.MakeOfferingUnavailable(service.Id);

        branch.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));
    }

    [Fact]
    public void SetCustomPrice_on_an_unavailable_service_should_keep_it_unavailable()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.MakeOfferingUnavailable(service.Id);

        branch.SetCustomPrice(service, Money.From(80m));

        branch.Offers(service.Id).ShouldBeFalse();
        branch.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));
    }

    [Fact]
    public void MakeOfferingAvailable_should_drop_the_override_when_nothing_else_differs()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.MakeOfferingUnavailable(service.Id);

        branch.MakeOfferingAvailable(service.Id);

        branch.Offers(service.Id).ShouldBeTrue();
        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void MakeOfferingAvailable_should_keep_the_override_when_there_is_a_custom_price()
    {
        var branch = CreateBranch();
        var service = FixedService();
        branch.SetCustomPrice(service, Money.From(80m));
        branch.MakeOfferingUnavailable(service.Id);

        branch.MakeOfferingAvailable(service.Id);

        branch.Offers(service.Id).ShouldBeTrue();
        branch.GetCustomPrice(service.Id).ShouldBe(Money.From(80m));
        branch.OfferingOverrides.Count.ShouldBe(1);
    }

    [Fact]
    public void MakeOfferingAvailable_without_an_override_should_do_nothing()
    {
        var branch = CreateBranch();

        branch.MakeOfferingAvailable(Guid.CreateVersion7());

        branch.OfferingOverrides.ShouldBeEmpty();
    }

    [Fact]
    public void Overrides_should_be_independent_per_service()
    {
        var branch = CreateBranch();
        var haircut = FixedService(name: "Haircut");
        var beard = FixedService(name: "Beard");

        branch.SetCustomPrice(haircut, Money.From(80m));
        branch.MakeOfferingUnavailable(beard.Id);

        branch.GetCustomPrice(haircut.Id).ShouldBe(Money.From(80m));
        branch.Offers(haircut.Id).ShouldBeTrue();
        branch.GetCustomPrice(beard.Id).ShouldBeNull();
        branch.Offers(beard.Id).ShouldBeFalse();
        branch.OfferingOverrides.Count.ShouldBe(2);
    }

    [Fact]
    public void Empty_service_id_should_be_rejected()
    {
        var branch = CreateBranch();

        Should.Throw<DomainException>(() => branch.MakeOfferingUnavailable(Guid.Empty))
            .Code.ShouldBe("Branch.ServiceRequired");
        Should.Throw<DomainException>(() => branch.MakeOfferingAvailable(Guid.Empty))
            .Code.ShouldBe("Branch.ServiceRequired");
        Should.Throw<DomainException>(() => branch.ClearCustomPrice(Guid.Empty))
            .Code.ShouldBe("Branch.ServiceRequired");
    }

    [Fact]
    public void Overrides_with_the_same_content_should_be_equal()
    {
        var service = FixedService();
        var first = CreateBranch();
        var second = CreateBranch();

        first.SetCustomPrice(service, Money.From(80m));
        second.SetCustomPrice(service, Money.From(80m));

        first.OfferingOverrides[0].ShouldBe(second.OfferingOverrides[0]);

        second.MakeOfferingUnavailable(service.Id);

        first.OfferingOverrides[0].ShouldNotBe(second.OfferingOverrides[0]);
    }
}