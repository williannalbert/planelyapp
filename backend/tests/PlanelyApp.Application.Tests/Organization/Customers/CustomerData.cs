using NSubstitute;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Application.Tests.Organization.Customers;

internal static class CustomerData
{
    public const string ValidEmail = "ana@example.com";
    public const string ValidPhone = "+5511988887777";
    public const string ValidCpf = "52998224725";
    public const string OtherCpf = "11144477735";

    /// <summary>A customer with e-mail and phone. The registration event is cleared: it is not what the tests look at.</summary>
    public static Customer NewCustomer(Guid? companyId = null, string? name = "Ana Souza")
    {
        var customer = Customer.Register(
            companyId ?? Guid.NewGuid(), Email.Create(ValidEmail), Phone.Create(ValidPhone), name);
        customer.ClearDomainEvents();

        return customer;
    }

    /// <summary>A repository that finds this customer (or none) whatever the id.</summary>
    public static ICustomerRepository RepositoryWith(Customer? customer)
    {
        var customers = Substitute.For<ICustomerRepository>();
        customers.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(customer);

        return customers;
    }
}