using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace PlanelyApp.Api.Documentation;

internal static class ApiDocumentationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "PlanelyApp API",
                Version = "v1",
                Description = "Scheduling for companies, branches and professionals.",
            };

            return Task.CompletedTask;
        }));

        return services;
    }

    /// <summary>The document lists every route, so it exists only in Development. Opening it elsewhere is a decision of 5.10.</summary>
    public static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapOpenApi().AllowAnonymous();
        endpoints.MapScalarApiReference().AllowAnonymous();

        return endpoints;
    }
}