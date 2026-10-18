using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class AddressTests
{
    private static Address Create(
        string? street = "Rua das Flores",
        string? number = "123",
        string? complement = null,
        string? district = "Centro",
        string? city = "São Paulo",
        string? state = "SP",
        string? postalCode = "01310-100") =>
        Address.Create(street, number, complement, district, city, state, postalCode);

    [Fact]
    public void Should_trim_and_normalize_fields()
    {
        var address = Create(
            street: " Rua das Flores ",
            complement: " Apto 4 ",
            state: " sp ",
            postalCode: "01310-100");

        address.Street.ShouldBe("Rua das Flores");
        address.Complement.ShouldBe("Apto 4");
        address.State.ShouldBe("SP");
        address.PostalCode.ShouldBe("01310100");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_complement_should_become_null(string? complement)
    {
        Create(complement: complement).Complement.ShouldBeNull();
    }

    [Fact]
    public void Required_fields_should_be_validated()
    {
        Should.Throw<DomainException>(() => Create(street: " ")).Code.ShouldBe("Address.StreetInvalid");
        Should.Throw<DomainException>(() => Create(number: null)).Code.ShouldBe("Address.NumberInvalid");
        Should.Throw<DomainException>(() => Create(district: "")).Code.ShouldBe("Address.DistrictInvalid");
        Should.Throw<DomainException>(() => Create(city: " ")).Code.ShouldBe("Address.CityInvalid");
    }

    [Fact]
    public void Too_long_fields_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Create(street: new string('a', 151)))
            .Code.ShouldBe("Address.StreetInvalid");
        Should.Throw<DomainException>(() => Create(complement: new string('a', 101)))
            .Code.ShouldBe("Address.ComplementInvalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("S")]
    [InlineData("SPP")]
    [InlineData("S1")]
    public void Invalid_state_should_be_rejected(string state)
    {
        Should.Throw<DomainException>(() => Create(state: state))
            .Code.ShouldBe("Address.StateInvalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("0131010")]
    [InlineData("013101000")]
    [InlineData("0131010A")]
    public void Invalid_postal_code_should_be_rejected(string postalCode)
    {
        Should.Throw<DomainException>(() => Create(postalCode: postalCode))
            .Code.ShouldBe("Address.PostalCodeInvalid");
    }

    [Fact]
    public void Addresses_with_same_values_should_be_equal()
    {
        Create(postalCode: "01310-100").ShouldBe(Create(postalCode: "01310100"));
        Create(number: "123").ShouldNotBe(Create(number: "124"));
    }
}