using System.Diagnostics.CodeAnalysis;

namespace PlanelyApp.Infrastructure.Messaging;

/// <summary>
/// The exchanges the monolith owns and which one each kind of message goes to. A message type is
/// "subject.what_happened": the subject picks the exchange and the whole type is the routing key.
/// The queues belong to whoever consumes; the monolith never declares them.
/// </summary>
public static class MessageTopology
{
    public const string SchedulingExchange = "scheduling.events";
    public const string CatalogExchange = "catalog.events";
    public const string OrganizationExchange = "organization.events";

    private static readonly Dictionary<string, string> ExchangeBySubject = new(StringComparer.Ordinal)
    {
        ["appointment"] = SchedulingExchange,
        ["promotion"] = CatalogExchange,
        ["customer"] = OrganizationExchange,
    };

    public static IReadOnlyList<string> Exchanges { get; } = [SchedulingExchange, CatalogExchange, OrganizationExchange];

    public static bool TryGetExchange(string type, [NotNullWhen(true)] out string? exchange)
    {
        exchange = null;

        var separator = type.IndexOf('.');

        return separator > 0
            && separator < type.Length - 1
            && ExchangeBySubject.TryGetValue(type[..separator], out exchange);
    }
}