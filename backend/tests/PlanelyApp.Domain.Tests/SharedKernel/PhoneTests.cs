using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;
using Shouldly;

namespace PlanelyApp.Domain.Tests.SharedKernel;

public class PhoneTests
{
    [Theory]
    [InlineData("+5511987654321", "+5511987654321")]
    [InlineData("+55 (11) 98765-4321", "+5511987654321")]
    [InlineData(" +1 202.555.0123 ", "+12025550123")]
    public void Should_normalize_to_e164(string input, string expected)
    {
        Phone.Create(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("11987654321")]       // missing "+"
    [InlineData("+0123456789")]       // country code cannot start with 0
    [InlineData("+5511abc54321")]     // letters
    [InlineData("+1234567")]          // 7 digits: too short
    [InlineData("+1234567890123456")] // 16 digits: too long
    public void Invalid_numbers_should_be_rejected(string input)
    {
        Should.Throw<DomainException>(() => Phone.Create(input))
            .Code.ShouldBe("Phone.Invalid");
    }

    [Fact]
    public void Null_should_be_rejected()
    {
        Should.Throw<DomainException>(() => Phone.Create(null))
            .Code.ShouldBe("Phone.Invalid");
    }

    [Fact]
    public void Same_number_with_different_formatting_should_be_equal()
    {
        Phone.Create("+55 11 98765-4321").ShouldBe(Phone.Create("+5511987654321"));
    }

    [Fact]
    public void Fifteen_digits_should_be_accepted()
    {
        Phone.Create("+123456789012345").Value.ShouldBe("+123456789012345");
    }
}