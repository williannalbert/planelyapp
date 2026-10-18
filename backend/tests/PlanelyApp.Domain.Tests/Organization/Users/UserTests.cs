using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.Organization.Users;

public class UserTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid BranchA = Guid.CreateVersion7();
    private static readonly Guid BranchB = Guid.CreateVersion7();

    private static Email Mail(string value = "maria@aurora.com") => Email.Create(value);

    private static User CreateStaff(UserRole role = UserRole.Receptionist) =>
        User.CreateForCompany(CompanyId, "kc-sub-1", "Maria", Mail(), role);

    [Fact]
    public void CreateForCompany_should_create_an_active_user_without_branch_access()
    {
        var user = User.CreateForCompany(CompanyId, "  kc-sub-1 ", "  Maria ", Mail(), UserRole.CompanyAdmin, Language.English);

        user.Id.Version.ShouldBe(7);
        user.CompanyId.ShouldBe(CompanyId);
        user.IdentityId.ShouldBe("kc-sub-1");
        user.Name.ShouldBe("Maria");
        user.Email.Value.ShouldBe("maria@aurora.com");
        user.Role.ShouldBe(UserRole.CompanyAdmin);
        user.PreferredLanguage.ShouldBe(Language.English);
        user.IsActive.ShouldBeTrue();
        user.AccessAllBranches.ShouldBeFalse();
        user.AllowedBranchIds.ShouldBeEmpty();
    }

    [Fact]
    public void CreatePlatformAdmin_should_create_a_user_without_company()
    {
        var user = User.CreatePlatformAdmin("kc-root", "Root", Mail("root@planely.app"));

        user.CompanyId.ShouldBeNull();
        user.Role.ShouldBe(UserRole.PlatformAdmin);
    }

    [Fact]
    public void CreateForCompany_with_empty_company_should_be_rejected()
    {
        Should.Throw<DomainException>(() =>
                User.CreateForCompany(Guid.Empty, "kc-sub-1", "Maria", Mail(), UserRole.Receptionist))
            .Code.ShouldBe("User.CompanyRequired");
    }

    [Fact]
    public void CreateForCompany_with_the_platform_admin_role_should_be_rejected()
    {
        Should.Throw<DomainException>(() => CreateStaff(UserRole.PlatformAdmin))
            .Code.ShouldBe("User.RoleInvalid");
        Should.Throw<DomainException>(() => CreateStaff((UserRole)99))
            .Code.ShouldBe("User.RoleInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_identity_id_should_be_rejected(string? identityId)
    {
        Should.Throw<DomainException>(() =>
                User.CreateForCompany(CompanyId, identityId, "Maria", Mail(), UserRole.Receptionist))
            .Code.ShouldBe("User.IdentityIdInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string? name)
    {
        Should.Throw<DomainException>(() =>
                User.CreateForCompany(CompanyId, "kc-sub-1", name, Mail(), UserRole.Receptionist))
            .Code.ShouldBe("User.NameInvalid");
    }

    [Fact]
    public void ChangeRole_should_switch_between_company_roles()
    {
        var user = CreateStaff();

        user.ChangeRole(UserRole.Professional);

        user.Role.ShouldBe(UserRole.Professional);
    }

    [Fact]
    public void ChangeRole_should_not_allow_the_platform_admin_role_in_or_out()
    {
        var staff = CreateStaff();
        var admin = User.CreatePlatformAdmin("kc-root", "Root", Mail("root@planely.app"));

        Should.Throw<DomainException>(() => staff.ChangeRole(UserRole.PlatformAdmin))
            .Code.ShouldBe("User.RoleInvalid");
        Should.Throw<DomainException>(() => admin.ChangeRole(UserRole.CompanyAdmin))
            .Code.ShouldBe("User.RoleInvalid");
        staff.Role.ShouldBe(UserRole.Receptionist);
    }

    [Fact]
    public void SetBranchAccess_with_all_branches_should_allow_any_branch()
    {
        var user = CreateStaff();

        user.SetBranchAccess(true);

        user.AccessAllBranches.ShouldBeTrue();
        user.CanAccess(BranchA).ShouldBeTrue();
        user.CanAccess(Guid.CreateVersion7()).ShouldBeTrue();
    }

    [Fact]
    public void SetBranchAccess_with_a_list_should_allow_only_those_branches()
    {
        var user = CreateStaff();

        user.SetBranchAccess(false, [BranchA, BranchA]);

        user.AccessAllBranches.ShouldBeFalse();
        user.AllowedBranchIds.Count.ShouldBe(1);
        user.CanAccess(BranchA).ShouldBeTrue();
        user.CanAccess(BranchB).ShouldBeFalse();
    }

    [Fact]
    public void SetBranchAccess_should_replace_the_previous_configuration()
    {
        var user = CreateStaff();
        user.SetBranchAccess(false, [BranchA]);

        user.SetBranchAccess(false, [BranchB]);
        user.CanAccess(BranchA).ShouldBeFalse();
        user.CanAccess(BranchB).ShouldBeTrue();

        user.SetBranchAccess(true, [BranchA]);
        user.AllowedBranchIds.ShouldBeEmpty();

        user.SetBranchAccess(false);
        user.CanAccess(BranchA).ShouldBeFalse();
    }

    [Fact]
    public void SetBranchAccess_with_an_empty_branch_id_should_be_rejected_and_change_nothing()
    {
        var user = CreateStaff();
        user.SetBranchAccess(false, [BranchA]);

        Should.Throw<DomainException>(() => user.SetBranchAccess(false, [BranchB, Guid.Empty]))
            .Code.ShouldBe("User.BranchInvalid");

        user.CanAccess(BranchA).ShouldBeTrue();
        user.CanAccess(BranchB).ShouldBeFalse();
    }

    [Fact]
    public void Platform_admin_should_not_have_branch_access()
    {
        var admin = User.CreatePlatformAdmin("kc-root", "Root", Mail("root@planely.app"));

        Should.Throw<DomainException>(() => admin.SetBranchAccess(true))
            .Code.ShouldBe("User.BranchAccessNotApplicable");
        admin.CanAccess(BranchA).ShouldBeFalse();
    }

    [Fact]
    public void Inactive_user_should_not_access_any_branch()
    {
        var user = CreateStaff();
        user.SetBranchAccess(true);

        user.Deactivate();

        user.IsActive.ShouldBeFalse();
        user.CanAccess(BranchA).ShouldBeFalse();

        user.Activate();
        user.CanAccess(BranchA).ShouldBeTrue();
    }

    [Fact]
    public void UpdateDetails_should_change_name_email_and_language()
    {
        var user = CreateStaff();

        user.UpdateDetails("Maria Lima", Mail("maria.lima@aurora.com"), Language.Spanish);

        user.Name.ShouldBe("Maria Lima");
        user.Email.Value.ShouldBe("maria.lima@aurora.com");
        user.PreferredLanguage.ShouldBe(Language.Spanish);
    }

    [Fact]
    public void Invalid_update_should_leave_the_user_unchanged()
    {
        var user = CreateStaff();

        Should.Throw<DomainException>(() => user.UpdateDetails(" ", Mail("outro@aurora.com"), Language.Spanish))
            .Code.ShouldBe("User.NameInvalid");

        user.Name.ShouldBe("Maria");
        user.Email.Value.ShouldBe("maria@aurora.com");
        user.PreferredLanguage.ShouldBeNull();
    }
}