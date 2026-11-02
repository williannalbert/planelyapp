using NSubstitute;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories.Rename;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.Application.Tests.Catalog;

public class RenameServiceCategoryTests
{
    [Fact]
    public async Task Renaming_should_change_the_name_trimmed()
    {
        var category = CatalogData.NewCategory();
        var handler = new RenameServiceCategoryHandler(CatalogData.CategoriesWith(category));

        var result = await handler.HandleAsync(
            new RenameServiceCategoryCommand(category.Id, "  Hair and beard "), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        category.Name.ShouldBe("Hair and beard");
    }

    [Fact]
    public async Task A_name_used_by_another_category_should_be_a_conflict_and_leave_the_name_alone()
    {
        var category = CatalogData.NewCategory();
        var categories = CatalogData.CategoriesWith(category);
        categories.ExistsByNameAsync("Beard", category.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new RenameServiceCategoryHandler(categories).HandleAsync(
            new RenameServiceCategoryCommand(category.Id, "Beard"), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("ServiceCategory.NameAlreadyExists");
        category.Name.ShouldBe("Hair");
    }

    [Fact]
    public async Task The_category_itself_should_be_left_out_of_the_name_lookup()
    {
        var category = CatalogData.NewCategory();
        var categories = CatalogData.CategoriesWith(category);

        await new RenameServiceCategoryHandler(categories).HandleAsync(
            new RenameServiceCategoryCommand(category.Id, " Hair "), TestContext.Current.CancellationToken);

        await categories.Received(1).ExistsByNameAsync("Hair", category.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_category_should_be_not_found()
    {
        var handler = new RenameServiceCategoryHandler(CatalogData.CategoriesWith(null));

        var result = await handler.HandleAsync(
            new RenameServiceCategoryCommand(Guid.NewGuid(), "Hair"), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("ServiceCategory.NotFound");
    }

    [Fact]
    public async Task A_name_above_the_limit_should_be_refused_by_the_domain_and_leave_the_name_alone()
    {
        var category = CatalogData.NewCategory();
        var handler = new RenameServiceCategoryHandler(CatalogData.CategoriesWith(category));

        var exception = await Should.ThrowAsync<DomainException>(() => handler.HandleAsync(
            new RenameServiceCategoryCommand(category.Id, new string('x', 101)), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("ServiceCategory.NameInvalid");
        category.Name.ShouldBe("Hair");
    }

    [Fact]
    public async Task A_valid_command_should_pass_the_validator()
    {
        var problems = await TestValidation.RunAsync(
            new RenameServiceCategoryValidator(), new RenameServiceCategoryCommand(Guid.NewGuid(), "Hair"));

        problems.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_values_should_be_reported_as_required()
    {
        var problems = await TestValidation.RunAsync(
            new RenameServiceCategoryValidator(), new RenameServiceCategoryCommand(Guid.Empty, " "));

        problems.ShouldBe(
            ["ServiceCategoryId:Validation.Required", "Name:Validation.Required"], ignoreOrder: true);
    }
}