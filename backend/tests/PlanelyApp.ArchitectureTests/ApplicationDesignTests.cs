using System.Reflection;
using NetArchTest.Rules;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Domain.SeedWork;
using Shouldly;

namespace PlanelyApp.ArchitectureTests;

/// <summary>The design conventions of the Application, written as rules so they cannot erode in phase 5.</summary>
public class ApplicationDesignTests
{
    private const string Root = "PlanelyApp.Application";
    private const string AbstractionsNamespace = Root + ".Abstractions";
    private const string DecoratorsNamespace = AbstractionsNamespace + ".Decorators";
    private const string CatalogNamespace = Root + ".Catalog";
    private const string OrganizationNamespace = Root + ".Organization";
    private const string SchedulingNamespace = Root + ".Scheduling";
    private const string IdentityNamespace = Root + ".Identity";
    private const string ImportsNamespace = Root + ".Imports";

    private static readonly string[] Modules =
        [CatalogNamespace, OrganizationNamespace, SchedulingNamespace, IdentityNamespace, ImportsNamespace];

    private static readonly Assembly ApplicationAssembly = typeof(PlanelyApp.Application.AssemblyReference).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PlanelyApp.Infrastructure.AssemblyReference).Assembly;

    private static bool Implements(Type type, Type openInterface) =>
        type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == openInterface);

    private static bool IsHandlerContract(Type contract) =>
        contract.IsGenericType
        && (contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
            || contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

    private static List<Type> Requests() =>
        ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && (Implements(type, typeof(ICommand<>)) || Implements(type, typeof(IQuery<>))))
            .ToList();

    // The decorators are open generic types, so they are not counted here: only the real handlers.
    private static List<Type> Handlers() =>
        ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                           && type.GetInterfaces().Any(IsHandlerContract))
            .ToList();

    private static IEnumerable<Type> RequestsHandledBy(Type handler) =>
        handler.GetInterfaces().Where(IsHandlerContract).Select(contract => contract.GetGenericArguments()[0]);

    // A rule that selects no type would pass without checking anything.
    [Theory]
    [InlineData(AbstractionsNamespace)]
    [InlineData(CatalogNamespace)]
    [InlineData(OrganizationNamespace)]
    [InlineData(SchedulingNamespace)]
    [InlineData(IdentityNamespace)]
    [InlineData(ImportsNamespace)]
    public void Every_namespace_under_a_rule_should_contain_types(string namespaceName)
    {
        var types = Types.InAssembly(ApplicationAssembly).That().ResideInNamespace(namespaceName).GetTypes();

        types.ShouldNotBeEmpty($"No type found in {namespaceName}. Was the folder or namespace renamed?");
    }

    [Fact]
    public void Commands_and_queries_should_be_public_sealed_records()
    {
        var requests = Requests();

        requests.ShouldNotBeEmpty();

        // "<Clone>$" is the method only a record has.
        var offenders = requests
            .Where(type => !(type.IsPublic && type.IsSealed && type.GetMethod("<Clone>$") is not null))
            .Select(type => type.Name);

        offenders.ShouldBeEmpty("A request is a public sealed record: immutable data the Api builds and the handler reads.");
    }

    [Fact]
    public void Every_command_and_query_should_have_exactly_one_handler()
    {
        var handled = Handlers().SelectMany(RequestsHandledBy).ToList();

        var offenders = Requests()
            .Where(request => handled.Count(candidate => candidate == request) != 1)
            .Select(request => request.Name);

        offenders.ShouldBeEmpty("A request with no handler is never answered; with two, the dispatcher cannot choose.");
    }

    [Fact]
    public void A_handler_should_live_in_the_namespace_of_its_request()
    {
        var offenders = Handlers()
            .SelectMany(handler => RequestsHandledBy(handler)
                .Where(request => request.Namespace != handler.Namespace)
                .Select(request => $"{handler.Name} handles {request.Name}"));

        offenders.ShouldBeEmpty("The folder of a use case holds the request, its handler and its validator together.");
    }

    [Fact]
    public void A_handler_should_call_other_use_cases_through_the_dispatcher_and_not_through_other_handlers()
    {
        var handlers = Handlers();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var offenders = handlers
            .SelectMany(handler => handler.GetConstructors(all)
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => handlers.Contains(parameter.ParameterType) || IsHandlerContract(parameter.ParameterType))
                .Select(parameter => $"{handler.Name} receives {parameter.ParameterType.Name}"));

        offenders.ShouldBeEmpty("Going through the dispatcher keeps validation and the transaction of the other use case.");
    }

    [Fact]
    public void Only_the_decorators_should_use_the_unit_of_work()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().DoNotResideInNamespace(DecoratorsNamespace)
            .ShouldNot().HaveDependencyOn(typeof(IUnitOfWork).FullName!)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Requests_and_their_results_should_not_expose_entities_or_value_objects_of_the_domain()
    {
        var offenders = ApplicationAssembly.GetTypes()
            .Where(type => type.IsPublic
                           && (type.Name.EndsWith("Command") || type.Name.EndsWith("Query") || type.Name.EndsWith("Dto")
                               || type.Name.EndsWith("Response") || type.Name.EndsWith("Data")))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(property => TypesIn(property.PropertyType))
                .Where(used => typeof(Entity).IsAssignableFrom(used) || typeof(ValueObject).IsAssignableFrom(used))
                .Select(used => $"{type.Name} exposes {used.Name}"));

        offenders.ShouldBeEmpty("The Api sees primitives, enums and the *Data records, never the objects of the Domain.");
    }

    [Fact]
    public void Abstractions_should_not_depend_on_any_module() =>
        AssertNoDependency(AbstractionsNamespace, Modules);

    [Fact]
    public void Catalog_should_not_depend_on_any_other_module() =>
        AssertNoDependency(CatalogNamespace, ForbiddenFor(CatalogNamespace));

    [Fact]
    public void Organization_should_not_depend_on_any_other_module() =>
        AssertNoDependency(OrganizationNamespace, ForbiddenFor(OrganizationNamespace));

    [Fact]
    public void Scheduling_should_not_depend_on_any_other_module() =>
        AssertNoDependency(SchedulingNamespace, ForbiddenFor(SchedulingNamespace));

    [Fact]
    public void Identity_should_not_depend_on_any_other_module() =>
        AssertNoDependency(IdentityNamespace, ForbiddenFor(IdentityNamespace));

    // The import reuses the use cases of the others, so it is the one module that sits above them.
    [Fact]
    public void Imports_may_depend_only_on_Catalog_and_Organization() =>
        AssertNoDependency(ImportsNamespace, ForbiddenFor(ImportsNamespace, CatalogNamespace, OrganizationNamespace));

    [Fact]
    public void Application_should_not_depend_on_the_libraries_of_the_edges()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("ClosedXML", "Quartz", "Microsoft.AspNetCore", "Microsoft.Extensions.Hosting")
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Background_services_should_be_internal_and_sealed()
    {
        // By name, so the test does not need a reference to the hosting package.
        var services = InfrastructureAssembly.GetTypes()
            .Where(type => type.BaseType?.FullName == "Microsoft.Extensions.Hosting.BackgroundService")
            .ToList();

        services.ShouldNotBeEmpty("The outbox and the imports each have one.");
        services.Where(type => type.IsPublic || !type.IsSealed).Select(type => type.Name)
            .ShouldBeEmpty("What runs them is a registration method; nobody else needs the type.");
    }

    private static string[] ForbiddenFor(string module, params string[] allowed) =>
        Modules.Where(other => other != module && !allowed.Contains(other)).ToArray();

    private static void AssertNoDependency(string module, string[] forbidden)
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(module)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    // The type itself and the ones inside it: Guid? and IReadOnlyList<ImportErrorDto> are looked through.
    private static IEnumerable<Type> TypesIn(Type type)
    {
        yield return type;

        var inner = type.IsArray ? [type.GetElementType()!] : type.IsGenericType ? type.GetGenericArguments() : [];

        foreach (var used in inner.SelectMany(TypesIn))
        {
            yield return used;
        }
    }
}