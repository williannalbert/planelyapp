using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: the dispatcher gets the service provider of the request, so each handler lives as long as it.
        services.TryAddScoped<IDispatcher, Dispatcher>();

        return services;
    }
}