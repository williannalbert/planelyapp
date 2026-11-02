using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.SeedWork;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Catalog;

public class CreateServiceCategoryTests
{
    private readonly IServiceCategoryRepository _categories = Substitute.For<IServiceCategoryRepository>();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task A_new_category_should_be_added_to_the_company_of_the_request_with_the_name_trimmed()
    {
        ServiceCategory? added = null;
        _categories.When(repository => repository.Add(Arg.Any<ServiceCategory>()))
            .Do(call => added = call.Arg<ServiceCategory>());

        var result = await Handler().HandleAsync(
            new CreateServiceCategoryCommand("  Hair "), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        result.Value.ShouldBe(added!.Id);
        added.CompanyId.ShouldBe(_companyId);
        added.Name.ShouldBe("Hair");
    }

    [Fact]
    public async Task A_name_already_in_the_company_should_be_a_conflict()
    {
        _categories.ExistsByNameAsync("Hair", Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().HandleAsync(
            new CreateServiceCategoryCommand("Hair"), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceCategory.NameAlreadyExists");
        _categories.DidNotReceive().Add(Arg.Any<ServiceCategory>());
    }

    [Fact]
    public async Task The_name_should_be_looked_up_trimmed_and_without_leaving_any_category_out()
    {
        await Handler().HandleAsync(new CreateServiceCategoryCommand("  Hair "), TestContext.Current.CancellationToken);

        await _categories.Received(1).ExistsByNameAsync("Hair", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain()
    {
        var exception = await Should.ThrowAsync<DomainException>(() => Handler().HandleAsync(
            new CreateServiceCategoryCommand(new string('x', 101)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ServiceCategory.NameInvalid");
        _categories.DidNotReceive().Add(Arg.Any<ServiceCategory>());
    }

    [Fact]
    public async Task A_request_without_a_company_should_fail_loudly()
    {
        var handler = new CreateServiceCategoryHandler(_categories, Substitute.For<ITenantProvider>());

        await Should.ThrowAsync<InvalidOperationException>(() => handler.HandleAsync(
            new CreateServiceCategoryCommand("Hair"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceCategoryValidator(), new CreateServiceCategoryCommand("Hair"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_blank_name_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new CreateServiceCategoryValidator(), new CreateServiceCategoryCommand("   "));

        problems.ShouldBe(["Name:Validation.Required"], ignoreOrder: true);
    }

    private CreateServiceCategoryHandler Handler() => new(_categories, BranchData.TenantOf(_companyId));
}