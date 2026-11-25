using PlanelyApp.Api.Authentication;

namespace PlanelyApp.Api.Endpoints.Account;

internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/me", (CurrentUser user) =>
                TypedResults.Ok(new MeResponse(user.Id, user.Kind.ToString(), user.Role?.ToString(), user.CompanyId)))
            .WithName("GetMe")
            .WithTags("Account")
            .WithSummary("Who is acting, as the Api sees it.");

        return endpoints;
    }
}

/// <summary>Role and kind go as text on purpose: the choice of how enums are written is made once, with the first real endpoints (5.5).</summary>
internal sealed record MeResponse(Guid Id, string Kind, string? Role, Guid? CompanyId);