using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Organization.Users.ResolveActor;

namespace PlanelyApp.Api.Authentication;

/// <summary>
/// Turns the subject of the token into the user of the platform. It runs only for an endpoint that asked for a signed-in
/// user: an open endpoint, an unknown route and an anonymous request never reach the database.
/// </summary>
internal sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser, IDispatcher dispatcher)
    {
        if (!NeedsTheUser(context))
        {
            await next(context);

            return;
        }

        var subject = context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            // Keycloak always sends it. A token without it is not one of ours.
            await context.ChallengeAsync(ApiAuthenticationExtensions.StaffScheme);

            return;
        }

        var result = await dispatcher.QueryAsync(new ResolveStaffActorQuery(subject), context.RequestAborted);

        if (result.IsFailure)
        {
            await new ErrorResult(result.Error).ExecuteAsync(context);

            return;
        }

        currentUser.Use(result.Value);

        await next(context);
    }

    private static bool NeedsTheUser(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.GetEndpoint() is { } endpoint
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
}