using NSubstitute;
using PlanelyApp.Application.Organization.Branches.Create;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Tests.Organization.Branches;

internal static class BranchData
{
    public const string ValidPhone = "+551133334444";
    public const string ValidTimeZone = "America/Sao_Paulo";

    public static AddressData NewAddress() =>
        new("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "SP", "01310100");

    public static Branch NewBranch(Guid? companyId = null, string name = "Centro") =>
        Branch.Create(
            companyId ?? Guid.NewGuid(), name, NewAddress().ToDomain(), Phone.Create(ValidPhone), ValidTimeZone);

    public static CreateBranchCommand CreateCommand(string name = "Centro") =>
        new(name, NewAddress(), ValidPhone, ValidTimeZone);

    /// <summary>A repository that finds this branch (or none) whatever the id.</summary>
    public static IBranchRepository RepositoryWith(Branch? branch)
    {
        var branches = Substitute.For<IBranchRepository>();
        branches.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(branch);

        return branches;
    }

    public static ITenantProvider TenantOf(Guid companyId)
    {
        var tenant = Substitute.For<ITenantProvider>();
        tenant.CompanyId.Returns((Guid?)companyId);

        return tenant;
    }
}