using System.Globalization;
using PlanelyApp.Api.Authentication;
using PlanelyApp.Api.Errors;
using PlanelyApp.Application.Abstractions.Identity;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Infrastructure.Tenancy;

namespace PlanelyApp.Api.Tenancy;

/// <summary>
/// The company of the request is the one in the route (RN23), and a staff member sees only the branches of its list (RN11).
/// It runs after <see cref="CurrentUserMiddleware"/>: when the user is known the ids of the route are compared with it; an open
/// endpoint has no user to compare, but still gets the tenant (the customer who logs in needs to be in a company).
/// </summary>
internal sealed class TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
{
    private const string CompanyKey = "companyId";
    private const string BranchKey = "branchId";

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser, TenantContext tenant)
    {
        if (context.GetEndpoint() is null)
        {
            await next(context);

            return;
        }

        var companyId = ReadId(context, CompanyKey, out var companyIsValid);
        var branchId = ReadId(context, BranchKey, out var branchIsValid);

        if (!companyIsValid || !branchIsValid)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        if (currentUser.IsResolved)
        {
            if (companyId is { } company && !currentUser.MayActInCompany(company))
            {
                await RefuseAsync(context, currentUser, "Company.AccessDenied", companyId, branchId);

                return;
            }

            if (branchId is { } branch && currentUser.Kind == ActorKind.Staff && !currentUser.CanAccessBranch(branch))
            {
                await RefuseAsync(context, currentUser, "Branch.AccessDenied", companyId, branchId);

                return;
            }
        }

        if (companyId is { } id)
        {
            tenant.Use(id);
        }

        await next(context);
    }

    /// <summary>No value is fine (the route has no such id). A value that is not an id, or is the empty one, is not.</summary>
    private static Guid? ReadId(HttpContext context, string key, out bool isValid)
    {
        isValid = true;

        if (context.GetRouteValue(key) is not { } value)
        {
            return null;
        }

        if (Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var id) && id != Guid.Empty)
        {
            return id;
        }

        isValid = false;

        return null;
    }

    private async Task RefuseAsync(
        HttpContext context,
        CurrentUser currentUser,
        string code,
        Guid? companyId,
        Guid? branchId)
    {
        // Ids and the code only: no name, e-mail or token.
        logger.LogInformation(
            "Access refused: {Code}. User {UserId}, company {CompanyId}, branch {BranchId}.",
            code, currentUser.Id, companyId, branchId);

        await new ErrorResult(Error.Forbidden(code)).ExecuteAsync(context);
    }
}