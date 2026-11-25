using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Results;

namespace PlanelyApp.Api.Roles;

/// <summary>
/// Closed by default: an endpoint that asks for a signed-in user and declares no <see cref="RoleRule"/> is refused, and the
/// mistake is logged as an error. It runs after the tenancy and only when the user was resolved, so an open endpoint never gets here.
/// </summary>
internal sealed class RoleMiddleware(RequestDelegate next, ILogger<RoleMiddleware> logger)
{
    private const string NotAllowed = "User.RoleNotAllowed";

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser)
    {
        if (!currentUser.IsResolved)
        {
            await next(context);

            return;
        }

        var endpoint = context.GetEndpoint();
        var rule = endpoint?.Metadata.GetMetadata<RoleRule>();

        if (rule is null)
        {
            logger.LogError(
                "The endpoint {Endpoint} has no role rule, so it was closed. Declare RequireRoles or RequireAnyStaff (or AllowAnonymous).",
                endpoint?.DisplayName);

            await RefuseAsync(context);

            return;
        }

        if (!rule.Allows(currentUser.Role))
        {
            // Ids, the code and the role only: no name, e-mail or token.
            logger.LogInformation(
                "Access refused: {Code}. User {UserId}, role {Role}.", NotAllowed, currentUser.Id, currentUser.Role);

            await RefuseAsync(context);

            return;
        }

        await next(context);
    }

    private static Task RefuseAsync(HttpContext context) =>
        new ErrorResult(Error.Forbidden(NotAllowed)).ExecuteAsync(context);
}