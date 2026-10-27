using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceCategories;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.Scheduling.Appointments;
using PlanelyApp.Domain.SeedWork;
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

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServiceCategoryRepository, ServiceCategoryRepository>();
        services.AddScoped<IServiceOfferingRepository, ServiceOfferingRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();

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