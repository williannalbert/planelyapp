using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.Get;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Application.Tests.Organization.Customers;

public class GetCustomerTests
{
    [Fact]
    public async Task The_customer_should_come_back_with_everything_and_the_cpf_formatted()
    {
        var customer = Customer.Register(
            Guid.NewGuid(),
            Email.Create(CustomerData.ValidEmail),
            Phone.Create(CustomerData.ValidPhone),
            "Ana Souza",
            acceptsMarketing: true,
            Language.English);
        customer.UpdateProfile("Ana Souza", Cpf.Create(CustomerData.ValidCpf), new DateOnly(1990, 5, 17), Language.Spanish);
        var accessedAt = new DateTime(2027, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        customer.RegisterAccess(accessedAt);

        var result = await new GetCustomerHandler(CustomerData.RepositoryWith(customer)).HandleAsync(
            new GetCustomerQuery(customer.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Id.ShouldBe(customer.Id);
        dto.Name.ShouldBe("Ana Souza");
        dto.Email.ShouldBe(CustomerData.ValidEmail);
        dto.Phone.ShouldBe(CustomerData.ValidPhone);
        dto.Cpf.ShouldBe("529.982.247-25");
        dto.BirthDate.ShouldBe(new DateOnly(1990, 5, 17));
        dto.LastAccessAt.ShouldBe(accessedAt);
        dto.AcceptsMarketing.ShouldBeTrue();
        dto.PreferredLanguage.ShouldBe("es");
    }

    [Fact]
    public async Task A_customer_with_only_a_phone_should_have_the_rest_empty()
    {
        var customer = Customer.Register(Guid.NewGuid(), null, Phone.Create(CustomerData.ValidPhone));

        var result = await new GetCustomerHandler(CustomerData.RepositoryWith(customer)).HandleAsync(
            new GetCustomerQuery(customer.Id), TestContext.Current.CancellationToken);

        var dto = result.Value;
        dto.Phone.ShouldBe(CustomerData.ValidPhone);
        dto.Name.ShouldBeNull();
        dto.Email.ShouldBeNull();
        dto.Cpf.ShouldBeNull();
        dto.BirthDate.ShouldBeNull();
        dto.LastAccessAt.ShouldBeNull();
        dto.PreferredLanguage.ShouldBeNull();
        dto.AcceptsMarketing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_customer_should_be_not_found()
    {
        var result = await new GetCustomerHandler(CustomerData.RepositoryWith(null)).HandleAsync(
            new GetCustomerQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Customer.NotFound");
    }
}