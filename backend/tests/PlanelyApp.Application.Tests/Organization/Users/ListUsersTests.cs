using NSubstitute;
using PlanelyApp.Application.Organization.Users;
using PlanelyApp.Application.Organization.Users.List;
using PlanelyApp.Domain.Organization.Users;
using Shouldly;
using BranchData = PlanelyApp.Application.Tests.Organization.Branches.BranchData;

namespace PlanelyApp.Application.Tests.Organization.Users;

public class ListUsersTests
{
    [Fact]
    public async Task What_the_read_model_lists_should_come_back_as_it_is_for_the_company_of_the_request()
    {
        var companyId = Guid.NewGuid();
        IReadOnlyList<UserSummaryDto> summaries =
            [new(Guid.NewGuid(), "Bia Lopes", UserData.ValidEmail, UserRole.Receptionist, true, true)];
        var readModel = Substitute.For<IUserReadModel>();
        readModel.ListAsync(companyId, null, null, Arg.Any<CancellationToken>()).Returns(summaries);

        var result = await new ListUsersHandler(readModel, BranchData.TenantOf(companyId))
            .HandleAsync(new ListUsersQuery(), TestContext.Current.CancellationToken);

        result.Value.ShouldBe(summaries, ignoreOrder: false);
    }

    [Fact]
    public async Task The_filters_should_reach_the_read_model()
    {
        var companyId = Guid.NewGuid();
        var readModel = Substitute.For<IUserReadModel>();

        await new ListUsersHandler(readModel, BranchData.TenantOf(companyId)).HandleAsync(
            new ListUsersQuery(UserRole.Professional, false), TestContext.Current.CancellationToken);

        await readModel.Received(1).ListAsync(companyId, UserRole.Professional, false, Arg.Any<CancellationToken>());
    }
}