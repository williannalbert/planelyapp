using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;

namespace PlanelyApp.Api.Tests;

/// <summary>
/// The texts of the Api (<c>Resources/Messages*.resx</c>) against the codes the code can produce. A code with no
/// text, a text with no code or a language that fell behind fails here, before a person sees an empty message.
/// </summary>
public partial class MessagesCatalogTests
{
    // String literals shaped like a code (Type.Problem) that are not codes: names of fields, enums, options and so on.
    private static readonly HashSet<string> NotCodes =
    [
        "AccessCodeVerification.Verified",
        "ActorKind.Staff",
        "CustomerSession.HashToken",
        "ImportOptions.PollingInterval",
        "Promotion.AppliesTo",
        "ReminderOptions.RunEvery",
        "Results.Error",
    ];

    // The Api turns a status with no body into Http.<Phrase of the status>.
    private static readonly string[] HttpCodes =
    [
        "Http.BadRequest", "Http.Unauthorized", "Http.Forbidden", "Http.NotFound", "Http.MethodNotAllowed",
        "Http.NotAcceptable", "Http.Conflict", "Http.UnsupportedMediaType", "Http.UnprocessableEntity",
        "Http.TooManyRequests", "Http.InternalServerError", "Http.BadGateway", "Http.ServiceUnavailable",
    ];

    private static readonly string[] Files = ["Messages.resx", "Messages.en.resx", "Messages.es.resx"];

    [GeneratedRegex("\"([A-Z][A-Za-z]+\\.[A-Z][A-Za-z]+)\"")]
    private static partial Regex CodeLiteral();

    [GeneratedRegex("\\{(\\d+)\\}")]
    private static partial Regex Placeholder();

    private static string BackendRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PlanelyApp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("PlanelyApp.slnx was not found above the test binaries.");
    }

    private static Dictionary<string, string> Load(string file)
    {
        var path = Path.Combine(BackendRoot(), "src", "PlanelyApp.Api", "Resources", file);

        return XDocument.Load(path).Root!.Elements("data").ToDictionary(
            data => (string)data.Attribute("name")!,
            data => (string)data.Element("value")!);
    }

    /// <summary>Every <c>"Type.Problem"</c> literal of the production code, minus the ones that are not codes.</summary>
    private static SortedSet<string> CodesInTheSource()
    {
        var codes = new SortedSet<string>();
        var separators = new[] { $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}" };

        foreach (var file in Directory.EnumerateFiles(Path.Combine(BackendRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (separators.Any(part => file.Contains(part)))
            {
                continue;
            }

            foreach (Match match in CodeLiteral().Matches(File.ReadAllText(file)))
            {
                var code = match.Groups[1].Value;

                if (!code.StartsWith("PlanelyApp.", StringComparison.Ordinal) && !NotCodes.Contains(code))
                {
                    codes.Add(code);
                }
            }
        }

        return codes;
    }

    [Fact]
    public void There_should_be_a_file_for_every_language()
    {
        foreach (var file in Files)
        {
            File.Exists(Path.Combine(BackendRoot(), "src", "PlanelyApp.Api", "Resources", file)).ShouldBeTrue(file);
        }
    }

    [Fact]
    public void Every_language_should_have_the_same_codes()
    {
        var portuguese = Load(Files[0]).Keys.ToHashSet();

        foreach (var file in Files.Skip(1))
        {
            var other = Load(file).Keys.ToHashSet();

            portuguese.Except(other).Order().ShouldBeEmpty($"{file} lacks these codes");
            other.Except(portuguese).Order().ShouldBeEmpty($"{file} has these codes that Messages.resx does not");
        }
    }

    [Fact]
    public void Every_code_of_the_source_should_have_a_text()
    {
        var keys = Load(Files[0]).Keys.ToHashSet();

        CodesInTheSource().Where(code => !keys.Contains(code)).ShouldBeEmpty(
            "add a text for each of these codes to the three .resx, or put the literal in NotCodes if it is not a code");
    }

    [Fact]
    public void Every_text_should_belong_to_a_code()
    {
        var known = CodesInTheSource().Concat(HttpCodes).ToHashSet();

        Load(Files[0]).Keys.Where(key => !known.Contains(key)).Order().ShouldBeEmpty(
            "these texts have no code in the source: remove them or fix the key");
    }

    [Fact]
    public void The_http_codes_should_have_texts()
    {
        var keys = Load(Files[0]).Keys.ToHashSet();

        HttpCodes.Where(code => !keys.Contains(code)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Messages.resx")]
    [InlineData("Messages.en.resx")]
    [InlineData("Messages.es.resx")]
    public void Every_text_should_be_filled_and_without_stray_spaces(string file)
    {
        var offenders = Load(file).Where(pair => pair.Value.Length == 0 || pair.Value != pair.Value.Trim()).Select(pair => pair.Key);

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void A_text_should_have_the_same_placeholders_in_every_language()
    {
        var portuguese = Load(Files[0]);

        foreach (var file in Files.Skip(1))
        {
            var other = Load(file);
            var offenders = portuguese
                .Where(pair => other.ContainsKey(pair.Key)
                    && !PlaceholdersOf(pair.Value).SetEquals(PlaceholdersOf(other[pair.Key])))
                .Select(pair => pair.Key);

            offenders.ShouldBeEmpty($"{file}: the placeholders ({{0}}, {{1}}...) must be the same as in Messages.resx");
        }

        static HashSet<string> PlaceholdersOf(string text) =>
            Placeholder().Matches(text).Select(match => match.Groups[1].Value).ToHashSet();
    }

    [Fact]
    public void A_translation_should_not_be_a_copy_of_the_portuguese_text()
    {
        var portuguese = Load(Files[0]);

        foreach (var file in Files.Skip(1))
        {
            var other = Load(file);

            portuguese.Where(pair => other.TryGetValue(pair.Key, out var text) && text == pair.Value)
                .Select(pair => pair.Key).ShouldBeEmpty($"{file} still has the Portuguese text of these codes");
        }
    }
}
