using System.Globalization;
using PlanelyApp.Application.Imports.Processing;
using Shouldly;

namespace PlanelyApp.Application.Tests.Imports;

public class ImportValuesTests
{
    [Theory]
    [InlineData("30", 30)]
    [InlineData("  45 ", 45)]
    [InlineData("0", 0)]
    public void A_whole_number_of_minutes_should_be_read(string text, int expected)
    {
        ImportValues.TryParseDuration(text, out var minutes).ShouldBeTrue();
        minutes.ShouldBe(expected);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    public void Anything_else_should_not_be_a_duration(string text) =>
        ImportValues.TryParseDuration(text, out _).ShouldBeFalse();

    // The expected value is a text because a decimal cannot be an attribute argument.
    [Theory]
    [InlineData("49,90", "49.90")]
    [InlineData("49.90", "49.90")]
    [InlineData("R$ 1.299,90", "1299.90")]
    [InlineData("1,299.90", "1299.90")]
    [InlineData("$ 20", "20")]
    [InlineData("-5", "-5")]
    [InlineData("1.500", "1.5")] // with only a dot, it is the decimal point: documented, not a bug
    public void A_price_should_be_read_in_both_formats(string text, string expected)
    {
        ImportValues.TryParsePrice(text, out var price).ShouldBeTrue();
        price.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12abc")]
    [InlineData("1.2.3")]
    [InlineData("")]
    public void Anything_else_should_not_be_a_price(string text) =>
        ImportValues.TryParsePrice(text, out _).ShouldBeFalse();

    [Theory]
    [InlineData("sim")]
    [InlineData("Sim")]
    [InlineData("yes")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("Sí")]
    public void Yes_should_be_read_in_the_three_languages(string text) => ImportValues.ParseYesNo(text).ShouldBe(true);

    [Theory]
    [InlineData("não")]
    [InlineData("No")]
    [InlineData("false")]
    [InlineData("0")]
    public void No_should_be_read_in_the_three_languages(string text) => ImportValues.ParseYesNo(text).ShouldBe(false);

    [Theory]
    [InlineData("talvez")]
    [InlineData("2")]
    public void Anything_else_should_be_neither_yes_nor_no(string text) => ImportValues.ParseYesNo(text).ShouldBeNull();

    [Theory]
    [InlineData("+55 11 99999-0001", "+55 11 99999-0001")]
    [InlineData("5511999990001", "+5511999990001")]
    [InlineData("55 11 99999-0001", "+5511999990001")]
    [InlineData("(11) 99999-0001", "(11) 99999-0001")]
    [InlineData("abc", "abc")]
    public void A_phone_with_the_country_code_should_get_its_plus_back(string text, string expected) =>
        ImportValues.NormalizePhone(text).ShouldBe(expected);
}