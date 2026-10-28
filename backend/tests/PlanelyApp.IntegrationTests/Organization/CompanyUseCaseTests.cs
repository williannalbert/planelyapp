using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Companies.Deactivate;
using PlanelyApp.Application.Organization.Companies.Register;
using PlanelyApp.Application.Organization.Companies.UpdateDetails;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>The whole way: dispatcher, decorators, handler, repository and MySQL.</summary>
public class CompanyUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Registering_a_company_should_save_it_with_the_default_policy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cnpj = OrganizationData.NewCnpj();

        Guid id;
        await using (var scope = mysql.CreateScope())
        {
            var result = await Dispatcher(scope).SendAsync(NewRegistration(cnpj), cancellationToken);

            result.IsSuccess.ShouldBeTrue();
            id = result.Value;
        }

        var company = await LoadAsync(id, cancellationToken);

        company.ShouldNotBeNull();
        company.TradeName.ShouldBe("Acme");
        company.Cnpj.ShouldBe(cnpj);
        company.DefaultLanguage.ShouldBe(Language.PortugueseBrazil);
        company.BookingPolicy.ShouldBe(BookingPolicy.Default);
        company.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Registering_the_same_cnpj_twice_should_be_a_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = NewRegistration(OrganizationData.NewCnpj());

        await using (var first = mysql.CreateScope())
        {
            (await Dispatcher(first).SendAsync(command, cancellationToken)).IsSuccess.ShouldBeTrue();
        }

        await using var second = mysql.CreateScope();
        var result = await Dispatcher(second).SendAsync(command, cancellationToken);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Company.CnpjAlreadyRegistered");
    }

    [Fact]
    public async Task Invalid_data_should_be_refused_and_nothing_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cnpj = OrganizationData.NewCnpj();

        await using var scope = mysql.CreateScope();
        var result = await Dispatcher(scope).SendAsync(
            NewRegistration(cnpj) with { Email = "not-an-email" }, cancellationToken);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Failures.ShouldHaveSingleItem().Field.ShouldBe("Email");
        (await scope.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .ExistsByCnpjAsync(cnpj, cancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Updating_the_details_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using (var scope = mysql.CreateScope(seeded.Id))
        {
            var result = await Dispatcher(scope).SendAsync(
                new UpdateCompanyDetailsCommand("New name", "New legal name", "new@acme.com", "+5511888880000", "en"),
                cancellationToken);

            result.IsSuccess.ShouldBeTrue();
        }

        var company = await LoadAsync(seeded.Id, cancellationToken);

        company.ShouldNotBeNull();
        company.TradeName.ShouldBe("New name");
        company.Email.Value.ShouldBe("new@acme.com");
        company.DefaultLanguage.ShouldBe(Language.English);
        company.Cnpj.ShouldBe(seeded.Cnpj);
    }

    [Fact]
    public async Task Deactivating_should_be_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seeded = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        await using (var scope = mysql.CreateScope(seeded.Id))
        {
            (await Dispatcher(scope).SendAsync(new DeactivateCompanyCommand(), cancellationToken))
                .IsSuccess.ShouldBeTrue();
        }

        (await LoadAsync(seeded.Id, cancellationToken))!.IsActive.ShouldBeFalse();
    }

    private static RegisterCompanyCommand NewRegistration(Cnpj cnpj) =>
        new("Acme", "Acme LTDA", cnpj.Value, "contact@acme.com", "+5511999990000", "pt-BR");

    private static IDispatcher Dispatcher(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IDispatcher>();

    private async Task<Company?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ICompanyRepository>()
            .GetByIdAsync(id, cancellationToken);
    }
}