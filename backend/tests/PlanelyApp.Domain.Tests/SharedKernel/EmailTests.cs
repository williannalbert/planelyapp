using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class EmailTests
{
    [Fact]
    public void Should_trim_and_lowercase()
    {
        Email.Create("  Ana.Souza@Exemplo.COM ").Value.ShouldBe("ana.souza@exemplo.com");
    }

    [Fact]
    public void Same_address_with_different_case_should_be_equal()
    {
        Email.Create("ANA@exemplo.com").ShouldBe(Email.Create("ana@EXEMPLO.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("@exemplo.com")]
    [InlineData("ana@exemplo")]
    [InlineData("ana@@exemplo.com")]
    [InlineData("ana souza@exemplo.com")]
    [InlineData("ana@.com")]
    [InlineData("ana@exemplo..com")]
    [InlineData("ana@exemplo.com.")]
    public void Invalid_addresses_should_be_rejected(string value)
    {
        Should.Throw<DomainException>(() => Email.Create(value))
            .Code.ShouldBe("Email.Invalid");
    }

    [Fact]
    public void Null_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Email.Create(null))
            .Code.ShouldBe("Email.Invalid");
    }

    [Fact]
    public void Address_longer_than_254_characters_should_be_rejected()
    {
        var tooLong = new string('a', 250) + "@x.com";

        Should.Throw<DomainException>(() => Email.Create(tooLong))
            .Code.ShouldBe("Email.Invalid");
    }
}