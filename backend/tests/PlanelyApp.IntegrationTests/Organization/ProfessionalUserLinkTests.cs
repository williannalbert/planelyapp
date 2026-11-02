using PlanelyApp.Application.Organization.Professionals.Get;
using PlanelyApp.Application.Organization.Professionals.LinkUser;
using PlanelyApp.Application.Organization.Professionals.Register;
using PlanelyApp.Application.Organization.Professionals.UnlinkUser;
using PlanelyApp.Application.Organization.Users.Create;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

public class ProfessionalUserLinkTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Linking_should_be_saved_and_unlinking_should_clear_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var professional = await RegisterProfessionalAsync(company.Id, "Ana Souza", cancellationToken);
        var user = await CreateUserAsync(company.Id, UserRole.Professional, cancellationToken);

        (await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(professional, user), cancellationToken))
            .IsSuccess.ShouldBeTrue();
        var linked = await UserIdOfAsync(company.Id, professional, cancellationToken);

        (await mysql.SendAsync(company.Id, new UnlinkProfessionalUserCommand(professional), cancellationToken))
            .IsSuccess.ShouldBeTrue();
        var unlinked = await UserIdOfAsync(company.Id, professional, cancellationToken);

        linked.ShouldBe(user);
        unlinked.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_should_belong_to_one_professional_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var first = await RegisterProfessionalAsync(company.Id, "Ana Souza", cancellationToken);
        var second = await RegisterProfessionalAsync(company.Id, "Bruno Alves", cancellationToken);
        var user = await CreateUserAsync(company.Id, UserRole.CompanyAdmin, cancellationToken);
        await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(first, user), cancellationToken);

        var taken = await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(second, user), cancellationToken);
        var again = await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(first, user), cancellationToken);

        taken.Error.Code.ShouldBe("Professional.UserAlreadyLinked");
        again.IsSuccess.ShouldBeTrue();
        (await UserIdOfAsync(company.Id, first, cancellationToken)).ShouldBe(user);
        (await UserIdOfAsync(company.Id, second, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Replacing_the_link_should_free_the_previous_user()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var first = await RegisterProfessionalAsync(company.Id, "Ana Souza", cancellationToken);
        var second = await RegisterProfessionalAsync(company.Id, "Bruno Alves", cancellationToken);
        var oldUser = await CreateUserAsync(company.Id, UserRole.Professional, cancellationToken);
        var newUser = await CreateUserAsync(company.Id, UserRole.Professional, cancellationToken);
        await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(first, oldUser), cancellationToken);

        var replaced = await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(first, newUser), cancellationToken);
        var reused = await mysql.SendAsync(company.Id, new LinkProfessionalUserCommand(second, oldUser), cancellationToken);

        replaced.IsSuccess.ShouldBeTrue();
        reused.IsSuccess.ShouldBeTrue();
        (await UserIdOfAsync(company.Id, first, cancellationToken)).ShouldBe(newUser);
        (await UserIdOfAsync(company.Id, second, cancellationToken)).ShouldBe(oldUser);
    }

    [Fact]
    public async Task Another_company_should_not_be_reachable_through_the_link()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var other = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var professional = await RegisterProfessionalAsync(company.Id, "Ana Souza", cancellationToken);
        var foreignUser = await CreateUserAsync(other.Id, UserRole.Professional, cancellationToken);

        var userOfOther = await mysql.SendAsync(
            company.Id, new LinkProfessionalUserCommand(professional, foreignUser), cancellationToken);
        var professionalOfOther = await mysql.SendAsync(
            other.Id, new LinkProfessionalUserCommand(professional, foreignUser), cancellationToken);
        var unlinkOfOther = await mysql.SendAsync(
            other.Id, new UnlinkProfessionalUserCommand(professional), cancellationToken);

        userOfOther.Error.Code.ShouldBe("User.NotFound");
        professionalOfOther.Error.Code.ShouldBe("Professional.NotFound");
        unlinkOfOther.Error.Code.ShouldBe("Professional.NotFound");
        (await UserIdOfAsync(company.Id, professional, cancellationToken)).ShouldBeNull();
    }

    private async Task<Guid> RegisterProfessionalAsync(Guid companyId, string name, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(companyId, new RegisterProfessionalCommand(name, "Cabeleireira"), cancellationToken)).Value;

    private async Task<Guid> CreateUserAsync(Guid companyId, UserRole role, CancellationToken cancellationToken) =>
        (await mysql.SendAsync(
            companyId, new CreateUserCommand("Bia Lopes", StaffData.NewEmail(), role, true), cancellationToken)).Value;

    private async Task<Guid?> UserIdOfAsync(Guid companyId, Guid professionalId, CancellationToken cancellationToken) =>
        (await mysql.QueryAsync(companyId, new GetProfessionalQuery(professionalId), cancellationToken)).Value.UserId;
}