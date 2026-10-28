using FluentValidation;
using PlanelyApp.Application.Abstractions.Decorators;

namespace PlanelyApp.Application.Tests;

internal static class TestValidation
{
    /// <summary>Runs the validator the way the pipeline does and describes what it found as "Field:Code".</summary>
    public static async Task<string[]> RunAsync<TRequest>(IValidator<TRequest> validator, TRequest request)
    {
        var error = await ValidationRunner.ValidateAsync<TRequest>(
            new[] { validator }, request, TestContext.Current.CancellationToken);

        return error is null
            ? []
            : error.Failures.Select(failure => $"{failure.Field}:{failure.Code}").ToArray();
    }
}