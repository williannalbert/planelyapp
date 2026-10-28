using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Decorators;
using PlanelyApp.Application.Abstractions.Events;
using FluentValidation;

namespace PlanelyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddDispatching()
            .AddUseCases(typeof(AssemblyReference).Assembly);

    /// <summary>The dispatchers. Scoped: they get the service provider of the request, so handlers live as long as it.</summary>
    internal static IServiceCollection AddDispatching(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDispatcher, Dispatcher>();
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Finds the handlers, event handlers and validators of an assembly and wraps the handlers in the decorators.
    /// Internal so the tests can run the same pipeline over their own handlers.
    /// </summary>
    internal static IServiceCollection AddUseCases(this IServiceCollection services, Assembly assembly)
    {
        // Not the generic classes: the decorators below also implement the handler interfaces.
        static bool IsConcrete(Type type) => !type.IsGenericTypeDefinition;

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)).Where(IsConcrete), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)).Where(IsConcrete), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)).Where(IsConcrete), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        // The last one applied is the outermost: log → validation → transaction → events → handler.
        // TryDecorate: it does nothing while there is no handler yet, instead of failing.
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(DomainEventsCommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionCommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandHandler<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandHandler<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryHandler<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryHandler<,>));

        return services;
    }
}