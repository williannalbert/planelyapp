using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.IntegrationTests;

internal static class MySqlFixtureExtensions
{
    /// <summary>Runs <paramref name="add"/> in a new scope of the given company (or none) and saves it.</summary>
    public static async Task SaveAsync(
        this MySqlFixture mysql,
        Guid? companyId,
        CancellationToken cancellationToken,
        Action<IServiceProvider> add)
    {
        await using var scope = mysql.CreateScope(companyId);

        add(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }
}