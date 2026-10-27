using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Domain.Organization.Users;

/// <summary>
/// A staff member. Authentication lives in Keycloak (linked by IdentityId);
/// the role and the branches the user can see live here.
/// </summary>
public sealed class User : AggregateRoot
{
    private const int NameMaxLength = 100;
    private const int IdentityIdMaxLength = 100;

    private readonly List<Guid> _allowedBranchIds = [];

    private User(
        Guid? companyId,
        string identityId,
        string name,
        Email email,
        UserRole role,
        Language? preferredLanguage)
    {
        CompanyId = companyId;
        IdentityId = identityId;
        Name = name;
        Email = email;
        Role = role;
        PreferredLanguage = preferredLanguage;
        IsActive = true;
    }

    /// <summary>Null only for the platform administrator.</summary>
    public Guid? CompanyId { get; }

    /// <summary>The Keycloak subject. Never changes.</summary>
    public string IdentityId { get; }

    public string Name { get; private set; }

    public Email Email { get; private set; }

    public UserRole Role { get; private set; }

    public bool AccessAllBranches { get; private set; }

    /// <summary>Only meaningful when AccessAllBranches is false.</summary>
    public IReadOnlyList<Guid> AllowedBranchIds => _allowedBranchIds.AsReadOnly();

    public Language? PreferredLanguage { get; private set; }

    public bool IsActive { get; private set; }

    public static User CreateForCompany(
        Guid companyId,
        string? identityId,
        string? name,
        Email email,
        UserRole role,
        Language? preferredLanguage = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainException("User.CompanyRequired");
        }

        if (role == UserRole.PlatformAdmin || !Enum.IsDefined(role))
        {
            throw new DomainException("User.RoleInvalid");
        }

        return new User(
            companyId,
            Guard.RequiredText(identityId, IdentityIdMaxLength, "User.IdentityIdInvalid"),
            Guard.RequiredText(name, NameMaxLength, "User.NameInvalid"),
            email,
            role,
            preferredLanguage);
    }

    public static User CreatePlatformAdmin(
        string? identityId,
        string? name,
        Email email,
        Language? preferredLanguage = null)
    {
        return new User(
            null,
            Guard.RequiredText(identityId, IdentityIdMaxLength, "User.IdentityIdInvalid"),
            Guard.RequiredText(name, NameMaxLength, "User.NameInvalid"),
            email,
            UserRole.PlatformAdmin,
            preferredLanguage);
    }

    public void UpdateDetails(string? name, Email email, Language? preferredLanguage)
    {
        var validName = Guard.RequiredText(name, NameMaxLength, "User.NameInvalid");

        Name = validName;
        Email = email;
        PreferredLanguage = preferredLanguage;
    }

    /// <summary>Changes the role among the company roles. The platform administrator role is fixed.</summary>
    public void ChangeRole(UserRole role)
    {
        if (Role == UserRole.PlatformAdmin || role == UserRole.PlatformAdmin || !Enum.IsDefined(role))
        {
            throw new DomainException("User.RoleInvalid");
        }

        Role = role;
    }

    /// <summary>
    /// Replaces the whole branch access: either every branch of the company or exactly the given ones.
    /// </summary>
    public void SetBranchAccess(bool accessAllBranches, IEnumerable<Guid>? branchIds = null)
    {
        if (CompanyId is null)
        {
            throw new DomainException("User.BranchAccessNotApplicable");
        }

        var ids = (branchIds ?? []).Distinct().ToList();

        if (!accessAllBranches && ids.Contains(Guid.Empty))
        {
            throw new DomainException("User.BranchInvalid");
        }

        AccessAllBranches = accessAllBranches;
        _allowedBranchIds.Clear();

        if (!accessAllBranches)
        {
            _allowedBranchIds.AddRange(ids);
        }
    }

    /// <summary>Whether the user can see and change data of the branch (RN11).</summary>
    public bool CanAccess(Guid branchId) =>
        IsActive
        && CompanyId is not null
        && (AccessAllBranches || _allowedBranchIds.Contains(branchId));

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}