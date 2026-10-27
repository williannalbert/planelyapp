using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.Infrastructure.Persistence;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Persistence;

/// <summary>
/// TenantIsolationTests proves the filter works on a throwaway entity. These prove that every real entity
/// is covered, so a new aggregate cannot be added without the filter by accident.
/// </summary>
public class TenantModelTests(MySqlFixture mysql)
{
    private static readonly MethodInfo SetMethod = typeof(DbContext).GetMethods().Single(method =>
        method.Name == nameof(DbContext.Set) && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);

    /// <summary>Entities that are not filtered by company, and why. A new one gets in here on purpose, with a reason.</summary>
    private static readonly Dictionary<Type, string> Exceptions = new()
    {
        [typeof(Company)] = "It is the tenant itself.",
        [typeof(User)] = "Found by the Keycloak subject before the company of the request is known; the platform administrator has no company.",
        [typeof(OutboxMessage)] = "The publisher serves every company.",
    };

    [Fact]
    public async Task Every_entity_of_a_company_should_be_a_tenant_entity_unless_it_is_a_documented_exception()
    {
        await using var scope = mysql.CreateScope(Guid.NewGuid());
        var model = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>().Model;

        var unprotected = model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .Select(entityType => entityType.ClrType)
            .Where(type => typeof(AggregateRoot).IsAssignableFrom(type) || type.GetProperty("CompanyId") is not null)
            .Where(type => !typeof(ITenantEntity).IsAssignableFrom(type) && !Exceptions.ContainsKey(type))
            .Select(type => type.Name)
            .ToList();

        unprotected.ShouldBeEmpty();
    }

    [Fact]
    public void The_exceptions_should_not_be_tenant_entities_already()
    {
        // If an exception becomes a tenant entity, it must leave the list: the list is the map of what is not filtered.
        Exceptions.Keys.Where(type => typeof(ITenantEntity).IsAssignableFrom(type)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_query_of_a_tenant_entity_should_carry_the_company_condition()
    {
        await using var scope = mysql.CreateScope(Guid.NewGuid());
        var context = scope.ServiceProvider.GetRequiredService<PlanelyDbContext>();

        var tenantTypes = context.Model.GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        tenantTypes.ShouldNotBeEmpty();

        var withoutCondition = new List<string>();

        foreach (var type in tenantTypes)
        {
            var query = (IQueryable)SetMethod.MakeGenericMethod(type).Invoke(context, null)!;
            var sql = query.ToQueryString();

            // The column also appears in the SELECT list, so only what comes after the first WHERE counts.
            var where = sql.IndexOf("WHERE", StringComparison.Ordinal);

            if (where < 0 || !sql[where..].Contains("company_id", StringComparison.Ordinal))
            {
                withoutCondition.Add(type.Name);
            }
        }

        withoutCondition.ShouldBeEmpty();
    }
}