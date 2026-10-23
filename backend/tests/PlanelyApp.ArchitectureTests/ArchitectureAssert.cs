using NetArchTest.Rules;
using Shouldly;
using ArchTestResult = NetArchTest.Rules.TestResult;

namespace PlanelyApp.ArchitectureTests;

internal static class ArchitectureAssert
{
    public static void Successful(ArchTestResult result)
    {
        var failingTypes = string.Join(", ", result.FailingTypeNames ?? []);
        result.IsSuccessful.ShouldBeTrue($"Architecture rule broken by: {failingTypes}");
    }
}