namespace PlanelyApp.Api.Correlation;

internal static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "PlanelyApp.CorrelationId";
    private const int MaxLength = 64;

    public static string? Of(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;

    public static void Set(HttpContext context, string correlationId) => context.Items[ItemKey] = correlationId;

    public static string New() => Guid.NewGuid().ToString("N");

    /// <summary>The id goes to logs and headers, so the one a caller sends is only trusted when it is short and plain.</summary>
    public static bool IsAcceptable(string? value) =>
        value is { Length: > 0 and <= MaxLength }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}