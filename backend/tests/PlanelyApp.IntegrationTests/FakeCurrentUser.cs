using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.IntegrationTests;

/// <summary>Who is acting in a scope. The Api builds it from the token (5.3); here the test says it before the request.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    private Guid? _id;

    public Guid Id => _id ?? throw new InvalidOperationException("The test did not say who is acting.");

    public ActorKind Kind { get; private set; }

    public UserRole? Role { get; private set; }

    public void UseStaff(Guid userId, UserRole role)
    {
        _id = userId;
        Kind = ActorKind.Staff;
        Role = role;
    }

    public void UseCustomer(Guid customerId)
    {
        _id = customerId;
        Kind = ActorKind.Customer;
        Role = null;
    }
}