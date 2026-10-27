using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.Infrastructure.Persistence;

namespace PlanelyApp.IntegrationTests.Messaging;

internal static class OutboxData
{
    // A whole hour: MySQL keeps microseconds and a DateTime keeps 100 nanoseconds, so it comes back exactly.
    public static readonly DateTime Start = DateTime.UtcNow.Date.AddHours(8);

    /// <summary>The table is shared by every test of the collection, and the processor reads all of it.</summary>
    public static async Task ClearAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();

        await scope.ServiceProvider.GetRequiredService<PlanelyDbContext>()
            .Database.ExecuteSqlRawAsync("DELETE FROM outbox_messages", cancellationToken);
    }

    /// <summary>What a use case does: adds through <see cref="IOutbox"/> and saves.</summary>
    public static async Task AddAsync(
        MySqlFixture mysql,
        Guid companyId,
        string type,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken,
        object? data = null,
        int version = 1)
    {
        await using var scope = mysql.CreateScope(companyId);

        scope.ServiceProvider.GetRequiredService<IOutbox>()
            .Add(type, companyId, data ?? new { appointmentId = Guid.NewGuid() }, occurredAtUtc, version);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    public static async Task<List<OutboxMessage>> AllAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<PlanelyDbContext>()
            .Set<OutboxMessage>()
            .AsNoTracking()
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }
}