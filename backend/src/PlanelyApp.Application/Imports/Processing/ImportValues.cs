using System.Globalization;

namespace PlanelyApp.Application.Imports.Processing;

/// <summary>Reads the texts of the cells as the values the use cases take. Always with the invariant culture.</summary>
internal static class ImportValues
{
    /// <summary>A phone typed only with digits and 12 or more of them is taken to carry the country code.</summary>
    private const int MinDigitsWithCountryCode = 12;

    private static readonly HashSet<string> Yes = ["sim", "s", "yes", "y", "true", "1", "x", "si", "verdadeiro"];
    private static readonly HashSet<string> No = ["nao", "n", "no", "false", "0", "falso"];

    /// <summary>A whole number of minutes. Whether it is a valid duration is the Domain's rule.</summary>
    public static bool TryParseDuration(string text, out int minutes) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out minutes);

    /// <summary>
    /// "49,90", "49.90", "R$ 1.299,90" and "1,299.90" are all read. With both separators the last one is the decimal point;
    /// with only one, a comma is the decimal point and a dot too ("1.500" is 1.5, not 1500).
    /// </summary>
    public static bool TryParsePrice(string text, out decimal price)
    {
        var cleaned = new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray())
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty)
            .Replace("€", string.Empty)
            .Replace("£", string.Empty);

        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            var decimalSeparator = lastDot > lastComma ? '.' : ',';
            var groupSeparator = decimalSeparator == '.' ? ',' : '.';
            cleaned = cleaned.Replace(groupSeparator.ToString(), string.Empty).Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            cleaned = cleaned.Replace(',', '.');
        }

        return decimal.TryParse(
            cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price);
    }

    /// <summary>Yes or no in the three languages; null when the text is neither.</summary>
    public static bool? ParseYesNo(string text)
    {
        var normalized = ImportSchemas.Normalize(text);

        if (Yes.Contains(normalized))
        {
            return true;
        }

        return No.Contains(normalized) ? false : null;
    }

    /// <summary>
    /// A spreadsheet stores a phone typed without the "+" as a number, so the sign is lost. Only digits with the country
    /// code (12 or more) get it back; anything else goes on as it is, and the Domain refuses what is not a phone.
    /// </summary>
    public static string NormalizePhone(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0 || trimmed[0] == '+')
        {
            return trimmed;
        }

        if (!trimmed.All(character => char.IsAsciiDigit(character) || character is ' ' or '-' or '(' or ')' or '.'))
        {
            return trimmed;
        }

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());

        return digits.Length >= MinDigitsWithCountryCode ? "+" + digits : trimmed;
    }
}