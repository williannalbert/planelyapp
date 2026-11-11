namespace PlanelyApp.Application.Imports.Processing;

/// <param name="Reason">Why the row was not imported (see <see cref="ImportReasons"/>). Null when it was.</param>
public sealed record ImportRowResult(bool Success, string? Reason = null)
{
    public static ImportRowResult Ok { get; } = new(true);

    public static ImportRowResult Failed(string reason) => new(false, reason);
}