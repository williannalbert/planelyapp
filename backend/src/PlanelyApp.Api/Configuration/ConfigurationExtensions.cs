namespace PlanelyApp.Api.Configuration;

internal static class ConfigurationExtensions
{
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"The connection string '{name}' is missing.");

    /// <summary>A section that must exist, bound to its options class. The class validates its own values when it is registered.</summary>
    public static T GetRequiredOptions<T>(this IConfiguration configuration, string section) where T : class =>
        configuration.GetSection(section).Get<T>()
        ?? throw new InvalidOperationException($"The configuration section '{section}' is missing.");
}