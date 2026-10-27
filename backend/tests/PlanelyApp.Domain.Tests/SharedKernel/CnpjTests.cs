using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void Numeric_cnpj_should_be_accepted_with_or_without_mask(string input)
    {
        var cnpj = Cnpj.Create(input);

        cnpj.Value.ShouldBe("11222333000181");
        cnpj.Formatted.ShouldBe("11.222.333/0001-81");
    }

    [Theory]
    [InlineData("12ABC34501DE35")]
    [InlineData("12.abc.345/01de-35")]
    public void Alphanumeric_cnpj_should_be_accepted_and_upper_cased(string input)
    {
        var cnpj = Cnpj.Create(input);

        cnpj.Value.ShouldBe("12ABC34501DE35");
        cnpj.Formatted.ShouldBe("12.ABC.345/01DE-35");
    }

    [Fact]
    public void Another_alphanumeric_cnpj_should_be_accepted()
    {
        Cnpj.Create("AB12CD34EF5602").Value.ShouldBe("AB12CD34EF5602");
    }

    [Theory]
    [InlineData("11222333000180")]    // wrong check digit
    [InlineData("12ABC34501DE36")]    // wrong check digit (alphanumeric)
    [InlineData("00000000000000")]    // repeated characters
    [InlineData("1122233300018")]     // 13 characters
    [InlineData("112223330001810")]   // 15 characters
    [InlineData("112223330001AB")]    // check digits must be numeric
    [InlineData("12ABC34501D#35")]    // invalid character
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_cnpj_should_be_rejected(string input)
    {
        Should.Throw<DomainException>(() => Cnpj.Create(input))
            .Code.ShouldBe("Cnpj.Invalid");
    }

    [Fact]
    public void Null_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Cnpj.Create(null))
            .Code.ShouldBe("Cnpj.Invalid");
    }

    [Fact]
    public void Same_cnpj_with_and_without_mask_should_be_equal()
    {
        Cnpj.Create("11.222.333/0001-81").ShouldBe(Cnpj.Create("11222333000181"));
    }
}