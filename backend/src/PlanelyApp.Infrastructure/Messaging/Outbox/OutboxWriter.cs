using System.Text.Json;
using System.Text.Json.Serialization;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure.Messaging.Outbox;

/// <summary>
/// Adds the message to the context of the request. It does not save: the SaveChanges of the use case saves the
/// change and the message in one transaction, which is the whole point of the pattern.
/// </summary>
internal sealed class OutboxWriter(PlanelyDbContext context, ITenantProvider tenantProvider) : IOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Add(string type, Guid companyId, object data, DateTime occurredAtUtc, int version = 1)
    {
        ArgumentNullException.ThrowIfNull(data);

        // A message nobody can route would sit in the table forever. Better to fail where the mistake was made.
        if (!MessageTopology.TryGetExchange(type, out _))
        {
            throw new ArgumentException($"No exchange is known for the message type '{type}'.", nameof(type));
        }

        // The table has no tenant filter, so this is the only check: a request of one company cannot leave
        // a message for another. A scope without company (a job that serves every company) may add.
        if (tenantProvider.CompanyId is { } current && current != companyId)
        {
            throw new TenantViolationException(
                $"A message of company {companyId} cannot be added from the scope of company {current}.");
        }

        var payload = JsonSerializer.Serialize(data, data.GetType(), JsonOptions);

        context.Set<OutboxMessage>().Add(OutboxMessage.Create(type, version, companyId, payload, occurredAtUtc));
    }
}