using NSubstitute;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Tests.Organization.Users;

internal static class UserData
{
    public const string ValidEmail = "bia@example.com";

    public static User NewUser(Guid? companyId = null, UserRole role = UserRole.Receptionist) =>
        User.CreateForCompany(
            companyId ?? Guid.NewGuid(), $"kc-{Guid.NewGuid():N}", "Bia Lopes", Email.Create(ValidEmail), role);

    /// <summary>A repository that finds this user (or none) whatever the company and the id.</summary>
    public static IUserRepository RepositoryWith(User? user)
    {
        var users = Substitute.For<IUserRepository>();
        users.GetByIdInCompanyAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(user);

        return users;
    }

    /// <summary>A provider whose creation and update work. (A substitute would answer null to a Result, which is sealed.)</summary>
    public static IIdentityProvider IdentityProvider(string createdId = "kc-new")
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.CreateUserAsync(Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(createdId));
        identity.UpdateUserAsync(Arg.Any<string>(), Arg.Any<IdentityProfile>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return identity;
    }
}