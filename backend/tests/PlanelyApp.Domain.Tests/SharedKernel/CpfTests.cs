using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void Valid_cpf_should_be_accepted_with_or_without_mask(string input)
    {
        var cpf = Cpf.Create(input);

        cpf.Value.ShouldBe("52998224725");
        cpf.Formatted.ShouldBe("529.982.247-25");
    }

    [Fact]
    public void Another_valid_cpf_should_be_accepted()
    {
        Cpf.Create("11144477735").Value.ShouldBe("11144477735");
    }

    [Theory]
    [InlineData("52998224726")]   // wrong check digit
    [InlineData("11111111111")]   // repeated digits
    [InlineData("5299822472")]    // 10 digits
    [InlineData("529982247255")]  // 12 digits
    [InlineData("5299822472A")]   // letter
    [InlineData("")]
    [InlineData("   ")]
    public void Invalid_cpf_should_be_rejected(string input)
    {
        Should.Throw<DomainException>(() => Cpf.Create(input))
            .Code.ShouldBe("Cpf.Invalid");
    }

    [Fact]
    public void Null_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Cpf.Create(null))
            .Code.ShouldBe("Cpf.Invalid");
    }

    [Fact]
    public void Same_cpf_with_and_without_mask_should_be_equal()
    {
        Cpf.Create("529.982.247-25").ShouldBe(Cpf.Create("52998224725"));
    }
}