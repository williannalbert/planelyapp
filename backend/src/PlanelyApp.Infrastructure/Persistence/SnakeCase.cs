using System.Text;

namespace PlanelyApp.Infrastructure.Persistence.Conventions;

/// <summary>Turns the C# names EF Core generates into snake_case: CompanyId -> company_id, Address_ZipCode -> address_zip_code.</summary>
public static class SnakeCase
{
    public static string From(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (!char.IsUpper(current))
            {
                builder.Append(current);
                continue;
            }

            if (i > 0 && StartsNewWord(name, i))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    private static bool StartsNewWord(string name, int index)
    {
        var previous = name[index - 1];

        if (previous == '_')
        {
            return false;
        }

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        // "URLPath": the P starts a word because it is followed by a lowercase letter.
        return char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}