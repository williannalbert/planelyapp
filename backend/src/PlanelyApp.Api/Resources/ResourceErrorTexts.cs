using System.Globalization;
using System.Resources;

namespace PlanelyApp.Api.Resources;

/// <summary>Reads Messages.resx (pt-BR), Messages.en.resx and Messages.es.resx. The key is the error code.</summary>
internal sealed class ResourceErrorTexts : IErrorTexts
{
    private static readonly ResourceManager Resources =
        new("PlanelyApp.Api.Resources.Messages", typeof(ResourceErrorTexts).Assembly);

    public string? Find(string code, IReadOnlyList<string>? arguments = null)
    {
        var culture = CultureInfo.CurrentUICulture;

        string? template;

        try
        {
            template = Resources.GetString(code, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }

        if (template is null || arguments is not { Count: > 0 })
        {
            return template;
        }

        try
        {
            return string.Format(culture, template, arguments.Cast<object>().ToArray());
        }
        catch (FormatException)
        {
            // A text with a broken placeholder must not turn a 4xx into a 500.
            return null;
        }
    }
}