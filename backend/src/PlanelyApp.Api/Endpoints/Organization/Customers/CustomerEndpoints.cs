using PlanelyApp.Api.Errors;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;
using PlanelyApp.Application.Organization.Customers.Get;
using PlanelyApp.Application.Organization.Customers.List;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Application.Organization.Customers.UpdateContact;
using PlanelyApp.Application.Organization.Customers.UpdateProfile;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Organization.Customers;

internal static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomers(this IEndpointRouteBuilder endpoints)
    {
        // Personal data (CPF, birth date, contacts): the counter and the administration. A professional sees the customer inside an
        // appointment, not in the register, and the platform administrator has no company to work in. Every endpoint has the same
        // roles, so the rule is the one of the group.
        var customers = endpoints.MapGroup("/companies/{companyId:guid}/customers")
            .WithTags("Customers")
            .RequireRoles(UserRole.CompanyAdmin, UserRole.Receptionist);

        customers
            .MapGet("/", async (
                string? search, int? page, int? pageSize, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(
                    new ListCustomersQuery(search, page ?? 1, pageSize ?? PageLimits.DefaultPageSize), cancellationToken)).ToHttp())
            .WithName("ListCustomers")
            .WithSummary("One page of the customers, with an optional search by name, e-mail or phone.");

        customers
            .MapPost("/", async (
                Guid companyId, RegisterCustomerCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(command, cancellationToken))
                    .ToCreated(id => $"/companies/{companyId}/customers/{id}"))
            .WithName("RegisterCustomer")
            .WithSummary("Registers a customer at the counter. An e-mail or a phone is needed.");

        var customer = customers.MapGroup("/{customerId:guid}");

        customer
            .MapGet("/", async (Guid customerId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.QueryAsync(new GetCustomerQuery(customerId), cancellationToken)).ToHttp())
            .WithName("GetCustomer")
            .WithSummary("Reads a customer, with CPF and birth date.");

        customer
            .MapPut("/contact", async (
                Guid customerId, CustomerContactBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateCustomerContactCommand(customerId, body.Email, body.Phone), cancellationToken)).ToNoContent())
            .WithName("UpdateCustomerContact")
            .WithSummary("Replaces the contacts of a customer. At least one must remain.");

        customer
            .MapPut("/profile", async (
                Guid customerId, CustomerProfileBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                (await dispatcher.SendAsync(
                    new UpdateCustomerProfileCommand(customerId, body.Name, body.Cpf, body.BirthDate, body.PreferredLanguage),
                    cancellationToken)).ToNoContent())
            .WithName("UpdateCustomerProfile")
            .WithSummary("Replaces the whole profile of a customer: a field left out is cleared.");

        customer
            .MapPut("/marketing-consent", async Task<IResult> (
                Guid customerId, MarketingConsentBody body, IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                // Left out, it would arrive as false, and a consent that nobody withdrew would be withdrawn.
                if (body.AcceptsMarketing is not { } accepts)
                {
                    return FieldProblems.Required(nameof(ChangeCustomerMarketingConsentCommand.AcceptsMarketing));
                }

                return (await dispatcher.SendAsync(
                    new ChangeCustomerMarketingConsentCommand(customerId, accepts), cancellationToken)).ToNoContent();
            })
            .WithName("ChangeCustomerMarketingConsent")
            .WithSummary("Gives or withdraws the marketing consent (LGPD).");

        return endpoints;
    }
}