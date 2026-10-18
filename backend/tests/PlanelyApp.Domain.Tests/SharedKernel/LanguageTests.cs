using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class LanguageTests
{
    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("EN", "en")]
    [InlineData(" es ", "es")]
    public void Should_accept_supported_languages_in_any_case(string input, string expected)
    {
        Language.Create(input).Code.ShouldBe(expected);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("pt")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unsupported_languages_should_be_rejected(string input)
    {
        Should.Throw<DomainException>(() => Language.Create(input))
            .Code.ShouldBe("Language.NotSupported");
    }

    [Fact]
    public void Null_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Language.Create(null))
            .Code.ShouldBe("Language.NotSupported");
    }

    [Fact]
    public void Default_should_be_brazilian_portuguese()
    {
        Language.Default.Code.ShouldBe("pt-BR");
    }

    [Fact]
    public void Same_code_should_be_equal()
    {
        Language.Create("pt-br").ShouldBe(Language.PortugueseBrazil);
    }
}