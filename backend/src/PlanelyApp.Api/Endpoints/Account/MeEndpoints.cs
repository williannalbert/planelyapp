using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Roles;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.Api.Endpoints.Account;

internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/me", (CurrentUser user) =>
                TypedResults.Ok(new MeResponse(user.Id, user.Kind, user.Role, user.CompanyId)))
            .WithName("GetMe")
            .WithTags("Account")
            .WithSummary("Who is acting, as the Api sees it.")
            .RequireAnyStaff();

        return endpoints;
    }
}

/// <summary>Kind and role are enums: they are written as names, like every enum of the Api (JSON options in <c>AddApiBasics</c>).</summary>
internal sealed record MeResponse(Guid Id, ActorKind Kind, UserRole? Role, Guid? CompanyId);