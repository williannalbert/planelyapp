using System.Globalization;
using System.Text;
using PlanelyApp.Domain.Imports;

namespace PlanelyApp.Application.Imports.Processing;

/// <summary>Which headers mean which column, in English, Portuguese and Spanish, and which columns each type needs.</summary>
internal static class ImportSchemas
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        [ImportColumns.Name] = ["name", "nome", "nombre", "servico", "servicio", "service", "cliente", "customer"],
        [ImportColumns.Duration] =
            ["duration", "duracao", "duracion", "minutes", "minutos", "durationminutes", "duracaominutos", "duracionminutos",
             "duracaomin", "duracionmin", "durationmin"],
        [ImportColumns.Price] = ["price", "preco", "precio", "valor", "baseprice", "precobase"],
        [ImportColumns.Description] = ["description", "descricao", "descripcion"],
        [ImportColumns.Category] = ["category", "categoria"],
        [ImportColumns.Email] = ["email", "emailaddress", "mail", "correo", "correoelectronico"],
        [ImportColumns.Phone] = ["phone", "telefone", "telefono", "celular", "whatsapp", "mobile", "tel"],
        [ImportColumns.Marketing] = ["marketing", "acceptsmarketing", "aceitamarketing", "aceptamarketing", "newsletter"],
        [ImportColumns.Language] = ["language", "idioma", "lang", "preferredlanguage", "idiomapreferido"],
    };

    private static readonly Dictionary<string, string> KeyByAlias = Aliases
        .SelectMany(pair => pair.Value.Select(alias => (Alias: alias, Key: pair.Key)))
        .ToDictionary(item => item.Alias, item => item.Key);

    /// <summary>Lower case, no accents, only letters and digits: "Duração (min)" becomes "duracaomin".</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);

        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>The first header of a column wins; headers that mean nothing are ignored.</summary>
    public static ImportColumnMap MapHeaders(IReadOnlyList<string> headers)
    {
        var columns = new Dictionary<string, int>();

        for (var index = 0; index < headers.Count; index++)
        {
            if (KeyByAlias.TryGetValue(Normalize(headers[index]), out var key))
            {
                columns.TryAdd(key, index);
            }
        }

        return new ImportColumnMap(columns);
    }

    /// <summary>The first required column the file does not have, or null when it has them all.</summary>
    public static string? FirstMissingColumn(ImportType type, ImportColumnMap map) => type switch
    {
        ImportType.Services when !map.Has(ImportColumns.Name) => ImportColumns.Name,
        ImportType.Services when !map.Has(ImportColumns.Duration) => ImportColumns.Duration,
        ImportType.Customers when !map.Has(ImportColumns.Email) && !map.Has(ImportColumns.Phone) => ImportColumns.EmailOrPhone,
        _ => null,
    };
}