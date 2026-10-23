using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace PlanelyApp.ArchitectureTests;

public class LayerTests
{
    private const string ApplicationNamespace = "PlanelyApp.Application";
    private const string InfrastructureNamespace = "PlanelyApp.Infrastructure";
    private const string ApiNamespace = "PlanelyApp.Api";

    private static readonly Assembly DomainAssembly = typeof(PlanelyApp.Domain.AssemblyReference).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PlanelyApp.Application.AssemblyReference).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PlanelyApp.Infrastructure.AssemblyReference).Assembly;

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
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "RabbitMQ")
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
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        ArchitectureAssert.Successful(result);
    }
}