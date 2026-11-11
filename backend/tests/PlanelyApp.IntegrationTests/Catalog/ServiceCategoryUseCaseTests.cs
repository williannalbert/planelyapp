using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Catalog.ServiceCategories;
using PlanelyApp.Application.Catalog.ServiceCategories.Create;
using PlanelyApp.Application.Catalog.ServiceCategories.List;
using PlanelyApp.Application.Catalog.ServiceCategories.Rename;
using PlanelyApp.IntegrationTests.Organization;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Catalog;

/// <summary>The whole way, as a request has it: dispatcher, decorators, handler, repository or read model, and MySQL.</summary>
public class ServiceCategoryUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Created_categories_should_be_listed_by_name_and_a_rename_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var tattoo = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("  Tattoo "), cancellationToken)).Value;
        var hair = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken)).Value;

        var renamed = await mysql.SendAsync(
            company.Id, new RenameServiceCategoryCommand(tattoo, "Body art"), cancellationToken);

        renamed.IsSuccess.ShouldBeTrue();
        var list = (await mysql.QueryAsync(company.Id, new ListServiceCategoriesQuery(), cancellationToken)).Value;
        list.ShouldBe(
            [new ServiceCategoryDto(tattoo, "Body art"), new ServiceCategoryDto(hair, "Hair")], ignoreOrder: false);
    }

    [Fact]
    public async Task A_name_should_be_unique_inside_the_company_but_free_in_another()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken);
        var beard = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Beard"), cancellationToken)).Value;

        var same = await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken);
        var otherCase = await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("HAIR"), cancellationToken);
        var inAnotherCompany = await mysql.SendAsync(other.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken);
        var renameTaken = await mysql.SendAsync(company.Id, new RenameServiceCategoryCommand(beard, "Hair"), cancellationToken);
        var renameSame = await mysql.SendAsync(company.Id, new RenameServiceCategoryCommand(beard, "Beard"), cancellationToken);
        var renameCase = await mysql.SendAsync(company.Id, new RenameServiceCategoryCommand(beard, "BEARD"), cancellationToken);

        same.Error.Type.ShouldBe(ErrorType.Conflict);
        same.Error.Code.ShouldBe("ServiceCategory.NameAlreadyExists");
        otherCase.Error.Code.ShouldBe("ServiceCategory.NameAlreadyExists");
        inAnotherCompany.IsSuccess.ShouldBeTrue();
        renameTaken.Error.Code.ShouldBe("ServiceCategory.NameAlreadyExists");
        renameSame.IsSuccess.ShouldBeTrue();
        renameCase.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Categories_of_another_company_should_not_be_listed_or_renamed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var hair = (await mysql.SendAsync(company.Id, new CreateServiceCategoryCommand("Hair"), cancellationToken)).Value;

        var list = await mysql.QueryAsync(other.Id, new ListServiceCategoriesQuery(), cancellationToken);
        var rename = await mysql.SendAsync(other.Id, new RenameServiceCategoryCommand(hair, "Mine"), cancellationToken);

        list.Value.ShouldBeEmpty();
        rename.Error.Type.ShouldBe(ErrorType.NotFound);
        rename.Error.Code.ShouldBe("ServiceCategory.NotFound");
    }
}