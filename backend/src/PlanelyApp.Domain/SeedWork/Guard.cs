namespace PlanelyApp.Domain.SeedWork;

internal static class Guard
{
    public static string RequiredText(string? value, int maxLength, string errorCode)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException(errorCode);
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, int maxLength, string errorCode) =>
        string.IsNullOrWhiteSpace(value) ? null : RequiredText(value, maxLength, errorCode);
}