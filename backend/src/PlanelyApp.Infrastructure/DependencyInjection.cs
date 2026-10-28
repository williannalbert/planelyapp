using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PlanelyApp.Application.Abstractions.Events;
using PlanelyApp.Application.Abstractions.Messaging;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Identity.AccessCodes;
using PlanelyApp.Domain.Identity.Sessions;
using PlanelyApp.Domain.Imports;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Infrastructure.Messaging;
using PlanelyApp.Infrastructure.Messaging.Outbox;
using PlanelyApp.Infrastructure.Messaging.RabbitMq;
using PlanelyApp.Infrastructure.Persistence;
using PlanelyApp.Infrastructure.Persistence.Interceptors;
using PlanelyApp.Infrastructure.Persistence.Repositories;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddPlanelyDbContext<PlanelyDbContext>(connectionString);

        // The use cases ask for IUnitOfWork and get the same context the repositories use.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PlanelyDbContext>());
        services.AddScoped<IDomainEventSource>(provider => provider.GetRequiredService<PlanelyDbContext>());

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServiceCategoryRepository, ServiceCategoryRepository>();
        services.AddScoped<IServiceOfferingRepository, ServiceOfferingRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAccessCodeRepository, AccessCodeRepository>();
        services.AddScoped<ICustomerSessionRepository, CustomerSessionRepository>();
        services.AddScoped<IDataImportRepository, DataImportRepository>();

        services.AddScoped<IOutbox, OutboxWriter>();

        return services;
    }

    /// <summary>
    /// The outbox processor and the background service that runs it. It still needs an <see cref="IMessagePublisher"/>:
    /// <see cref="AddRabbitMqPublisher"/> in production, a fake in tests.
    /// </summary>
    public static IServiceCollection AddOutboxProcessing(this IServiceCollection services, Action<OutboxOptions>? configure = null)
    {
        var options = new OutboxOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxPublisherService>();

        return services;
    }

    public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddLogging();
        services.AddSingleton<IMessagePublisher>(provider => new RabbitMqMessagePublisher(
            connectionString, provider.GetRequiredService<ILogger<RabbitMqMessagePublisher>>()));

        return services;
    }

    /// <summary>
    /// Registers a context with the interceptors and the tenant of the scope. Public so the tests can register
    /// their own context with exactly the same setup as production.
    /// </summary>
    public static IServiceCollection AddPlanelyDbContext<TContext>(this IServiceCollection services, string connectionString)
        where TContext : PlanelyDbContext
    {
        // TryAdd: a test that registered its own TimeProvider first keeps it.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditInterceptor>();
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantProvider>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<TenantWriteInterceptor>();

        services.AddDbContext<TContext>((provider, options) => options
            .UseMySql(connectionString, DatabaseServer.Version)
            .AddInterceptors(
                provider.GetRequiredService<TenantWriteInterceptor>(),
                provider.GetRequiredService<AuditInterceptor>()));

        return services;
    }
}