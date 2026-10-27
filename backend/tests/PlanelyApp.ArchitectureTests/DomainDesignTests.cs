using System.Reflection;
using NetArchTest.Rules;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.ArchitectureTests;

/// <summary>The design conventions of the Domain, written as rules so they cannot erode.</summary>
public class DomainDesignTests
{
    private static readonly Assembly DomainAssembly = typeof(PlanelyApp.Domain.AssemblyReference).Assembly;

    private static IEnumerable<Type> ConcreteTypesDerivedFrom(Type baseType) =>
        DomainAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && baseType.IsAssignableFrom(type));

    private static IEnumerable<Type> AggregateRoots => ConcreteTypesDerivedFrom(typeof(AggregateRoot));

    private static IEnumerable<(Type Repository, Type Root)> Repositories =>
        DomainAssembly.GetTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType
                                   && contract.GetGenericTypeDefinition() == typeof(IRepository<>))
                .Select(contract => (Repository: type, Root: contract.GetGenericArguments()[0])));

    [Fact]
    public void Entities_and_value_objects_should_be_created_only_through_factories()
    {
        var offenders = ConcreteTypesDerivedFrom(typeof(Entity))
            .Concat(ConcreteTypesDerivedFrom(typeof(ValueObject)))
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(type => type.Name);

        offenders.ShouldBeEmpty("Entities and value objects use private constructors and static factories.");
    }

    [Fact]
    public void Entities_and_value_objects_should_have_no_public_setters()
    {
        var offenders = ConcreteTypesDerivedFrom(typeof(Entity))
            .Concat(ConcreteTypesDerivedFrom(typeof(ValueObject)))
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPublic: true })
                .Select(property => $"{type.Name}.{property.Name}"));

        offenders.ShouldBeEmpty("State changes only through methods that validate first.");
    }

    [Fact]
    public void Domain_events_should_be_sealed()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(DomainEvent))
            .Should().BeSealed()
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Every_aggregate_root_should_have_exactly_one_repository()
    {
        var roots = Repositories.Select(pair => pair.Root).ToList();

        var without = AggregateRoots.Except(roots).Select(root => root.Name);
        var several = roots.GroupBy(root => root).Where(group => group.Count() > 1).Select(group => group.Key.Name);

        without.ShouldBeEmpty("Aggregate roots without a repository.");
        several.ShouldBeEmpty("Aggregate roots with more than one repository.");
    }

    [Fact]
    public void Repositories_should_be_named_after_their_aggregate()
    {
        var offenders = Repositories
            .Where(pair => pair.Repository.Name != $"I{pair.Root.Name}Repository")
            .Select(pair => pair.Repository.Name);

        offenders.ShouldBeEmpty("A repository for Customer is called ICustomerRepository.");
    }

    [Fact]
    public void Aggregate_roots_should_be_tenant_entities_except_Company_and_User()
    {
        // Company is the tenant itself. User has an optional company (the platform administrator has none),
        // so it is found by its Keycloak id before any tenant is known.
        var offenders = AggregateRoots
            .Where(root => root != typeof(Company) && root != typeof(User))
            .Where(root => !typeof(ITenantEntity).IsAssignableFrom(root))
            .Select(root => root.Name);

        offenders.ShouldBeEmpty("Without ITenantEntity the company filter of the infrastructure skips the aggregate.");
    }
}