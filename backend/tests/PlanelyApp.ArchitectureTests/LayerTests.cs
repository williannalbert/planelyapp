using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace PlanelyApp.ArchitectureTests;

public class LayerTests
{
    private const string ApplicationNamespace = "PlanelyApp.Application";
    private const string InfrastructureNamespace = "PlanelyApp.Infrastructure";
    private const string ApiNamespace = "PlanelyApp.Api";
    private const string RepositoriesNamespace = "PlanelyApp.Infrastructure.Persistence.Repositories";
    private const string ConfigurationsNamespace = "PlanelyApp.Infrastructure.Persistence.Configurations";

    // The packages that belong to the Infrastructure: EF Core, the MySQL drivers and RabbitMQ.
    private static readonly string[] ProviderNamespaces =
        ["Microsoft.EntityFrameworkCore", "Microting", "MySqlConnector", "RabbitMQ"];

    private static readonly Assembly DomainAssembly = typeof(PlanelyApp.Domain.AssemblyReference).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PlanelyApp.Application.AssemblyReference).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PlanelyApp.Infrastructure.AssemblyReference).Assembly;
    private static readonly Assembly ApiAssembly = typeof(PlanelyApp.Api.AssemblyReference).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Domain_should_not_depend_on_infrastructure_frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ProviderNamespaces.Append("Microsoft.AspNetCore").ToArray())
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_frameworks()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ProviderNamespaces)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Api_should_not_use_the_infrastructure_frameworks_directly()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ProviderNamespaces)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Repositories_should_be_internal_sealed_and_live_in_the_repositories_namespace()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Repository").And().AreNotAbstract()
            .Should().ResideInNamespace(RepositoriesNamespace)
            .And().NotBePublic()
            .And().BeSealed()
            .GetResult();

        ArchitectureAssert.Successful(result);
    }

    [Fact]
    public void Entity_configurations_should_be_internal_sealed_and_live_in_the_configurations_namespace()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Configuration").And().AreNotAbstract()
            .Should().ResideInNamespace(ConfigurationsNamespace)
            .And().NotBePublic()
            .And().BeSealed()
            .GetResult();

        ArchitectureAssert.Successful(result);
    }
}