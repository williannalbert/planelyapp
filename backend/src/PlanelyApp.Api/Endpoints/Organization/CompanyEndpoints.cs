using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Companies;
using PlanelyApp.Application.Organization.Companies.Activate;
using PlanelyApp.Application.Organization.Companies.Deactivate;
using PlanelyApp.Application.Organization.Companies.Get;
using PlanelyApp.Application.Organization.Companies.Register;
using PlanelyApp.Application.Organization.Companies.UpdateBookingPolicy;
using PlanelyApp.Application.Organization.Companies.UpdateDetails;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization;

internal static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanies(this IEndpointRouteBuilder endpoints)
    {
        var companies = endpoints.MapGroup("/companies").WithTags("Companies");

        companies
            .MapPost("/", async (RegisterCompanyCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken)).ToCreated(id => $"/companies/{id}"))
            .WithName("RegisterCompany")
            .WithSummary("Registers a company on the platform.")
            .RequireRoles(UserRole.PlatformAdmin);

        var company = companies.MapGroup("/{companyId:guid}");

        company
            .MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetCompanyQuery(), cancellationToken)).ToHttp())
            .WithName("GetCompany")
            .WithSummary("Reads a company.")
            .RequireAnyStaff();

        company
            .MapPut("/", async (UpdateCompanyDetailsCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken)).ToNoContent())
            .WithName("UpdateCompany")
            .WithSummary("Changes the data of a company. The CNPJ never changes.")
            .RequireRoles(UserRole.CompanyAdmin);

        company
            .MapPut("/booking-policy", async (BookingPolicyData policy, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new UpdateBookingPolicyCommand(policy), cancellationToken)).ToNoContent())
            .WithName("UpdateBookingPolicy")
            .WithSummary("Replaces the whole booking policy of a company.")
            .RequireRoles(UserRole.CompanyAdmin);

        company
            .MapPost("/activate", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new ActivateCompanyCommand(), cancellationToken)).ToNoContent())
            .WithName("ActivateCompany")
            .WithSummary("Activates a company.")
            .RequireRoles(UserRole.PlatformAdmin);

        company
            .MapPost("/deactivate", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(new DeactivateCompanyCommand(), cancellationToken)).ToNoContent())
            .WithName("DeactivateCompany")
            .WithSummary("Deactivates a company.")
            .RequireRoles(UserRole.PlatformAdmin);

        return endpoints;
    }
}