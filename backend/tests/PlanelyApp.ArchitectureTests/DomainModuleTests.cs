using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace PlanelyApp.ArchitectureTests;

/// <summary>
/// The direction of the dependencies between the bounded contexts of the Domain.
/// Allowed: Scheduling -> Organization -> Catalog, and everything -> SharedKernel -> SeedWork.
/// Identity and Imports stand alone: they refer to other aggregates by Id only.
/// </summary>
public class DomainModuleTests
{
    private const string Root = "PlanelyApp.Domain";
    private const string SeedWorkNamespace = Root + ".SeedWork";
    private const string SharedKernelNamespace = Root + ".SharedKernel";
    private const string OrganizationNamespace = Root + ".Organization";
    private const string CatalogNamespace = Root + ".Catalog";
    private const string SchedulingNamespace = Root + ".Scheduling";
    private const string IdentityNamespace = Root + ".Identity";
    private const string ImportsNamespace = Root + ".Imports";

    private const string PricingNamespace = SchedulingNamespace + ".Pricing";
    private const string AppointmentsNamespace = SchedulingNamespace + ".Appointments";
    private const string AvailabilityNamespace = SchedulingNamespace + ".Availability";

    private static readonly string[] Modules =
    [
        OrganizationNamespace, CatalogNamespace, SchedulingNamespace, IdentityNamespace, ImportsNamespace
    ];

    private static readonly Assembly DomainAssembly = typeof(PlanelyApp.Domain.AssemblyReference).Assembly;

    // A rule that selects no type would pass without checking anything.
    [Theory]
    [InlineData(SeedWorkNamespace)]
    [InlineData(SharedKernelNamespace)]
    [InlineData(OrganizationNamespace)]
    [InlineData(CatalogNamespace)]
    [InlineData(SchedulingNamespace)]
    [InlineData(IdentityNamespace)]
    [InlineData(ImportsNamespace)]
    [InlineData(PricingNamespace)]
    [InlineData(AppointmentsNamespace)]
    [InlineData(AvailabilityNamespace)]
    public void Every_namespace_under_a_rule_should_contain_types(string namespaceName)
    {
        var types = Types.InAssembly(DomainAssembly).That().ResideInNamespace(namespaceName).GetTypes();

        types.ShouldNotBeEmpty($"No type found in {namespaceName}. Was the folder or namespace renamed?");
    }

    [Fact]
    public void Every_type_should_live_in_a_known_namespace()
    {
        string[] known = [SeedWorkNamespace, SharedKernelNamespace, .. Modules];

        var offenders = DomainAssembly.GetTypes()
            .Where(type => type.DeclaringType is null)
            .Where(type => type.Namespace is not null && type.Namespace.StartsWith(Root))
            .Where(type => type != typeof(PlanelyApp.Domain.AssemblyReference))
            .Where(type => !known.Any(name => type.Namespace == name || type.Namespace!.StartsWith(name + ".")))
            .Select(type => type.FullName);

        offenders.ShouldBeEmpty("Types outside a module escape the dependency rules.");
    }

    [Fact]
    public void SeedWork_should_not_depend_on_anything_else_in_the_domain() =>
        AssertNoDependency(SeedWorkNamespace, [SharedKernelNamespace, .. Modules]);

    [Fact]
    public void SharedKernel_should_not_depend_on_any_module() =>
        AssertNoDependency(SharedKernelNamespace, Modules);

    [Fact]
    public void Catalog_should_not_depend_on_any_other_module() =>
        AssertNoDependency(CatalogNamespace, ForbiddenFor(CatalogNamespace));

    [Fact]
    public void Organization_may_depend_only_on_Catalog() =>
        AssertNoDependency(OrganizationNamespace, ForbiddenFor(OrganizationNamespace, CatalogNamespace));

    [Fact]
    public void Scheduling_may_depend_only_on_Organization_and_Catalog() =>
        AssertNoDependency(
            SchedulingNamespace,
            ForbiddenFor(SchedulingNamespace, OrganizationNamespace, CatalogNamespace));

    [Fact]
    public void Identity_should_not_depend_on_any_other_module() =>
        AssertNoDependency(IdentityNamespace, ForbiddenFor(IdentityNamespace));

    [Fact]
    public void Imports_should_not_depend_on_any_other_module() =>
        AssertNoDependency(ImportsNamespace, ForbiddenFor(ImportsNamespace));

    // Inside Scheduling the order is Pricing <- Appointments <- Availability.

    [Fact]
    public void Pricing_should_not_depend_on_Appointments_or_Availability() =>
        AssertNoDependency(PricingNamespace, [AppointmentsNamespace, AvailabilityNamespace]);

    [Fact]
    public void Appointments_should_not_depend_on_Availability() =>
        AssertNoDependency(AppointmentsNamespace, [AvailabilityNamespace]);

    private static string[] ForbiddenFor(string module, params string[] allowed) =>
        Modules.Where(other => other != module && !allowed.Contains(other)).ToArray();

    private static void AssertNoDependency(string module, string[] forbidden)
    {
        var result = Types.InAssembly(DomainAssembly)
            .That().ResideInNamespace(module)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }
}