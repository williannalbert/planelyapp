using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PlanelyApp.Api.Correlation;
using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Logging;
using PlanelyApp.Api.Resources;

namespace PlanelyApp.Api;

/// <summary>What every endpoint of the Api relies on: problem details, the exception handlers, the correlation id, JSON and the liveness check.</summary>
internal static class ApiBasicsExtensions
{
    public static IServiceCollection AddApiBasics(this IServiceCollection services)
    {
        services.AddApiLocalization();

        // An enum is its name in both directions; the number of a member is not part of the contract.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

        // For the answers nobody wrote (an unknown route, a wrong method, a 401 or a 403 of the framework).
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;

            problem.Extensions.TryAdd("code", ApiProblems.HttpCodeOf(status));

            if (problem.Extensions["code"] is string code)
            {
                var texts = context.HttpContext.RequestServices.GetRequiredService<IErrorTexts>();
                problem.Detail ??= texts.Find(code);
            }

            ApiProblems.Identify(context.HttpContext, problem);
        });

        // In this order: the first one that handles the exception wins.
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyConflictExceptionHandler>();
        services.AddExceptionHandler<IdentityProviderExceptionHandler>();
        services.AddExceptionHandler<BadRequestExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        services.AddHealthChecks();

        return services;
    }

    public static IApplicationBuilder UseApiBasics(this IApplicationBuilder app)
    {
        // The correlation id comes first so even a failure inside the other middleware has one.
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseApiLocalization();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }

    public static IEndpointRouteBuilder MapApiBasics(this IEndpointRouteBuilder endpoints)
    {
        // Liveness only: "the process answers". The readiness check (MySQL, RabbitMQ) is 5.9.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        return endpoints;
    }
}