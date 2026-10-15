using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Auth;

/// <summary>Keys used to store the logged-in user in the server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>
/// Session-based authorisation.
/// <c>[SessionAuthorize]</c> = any logged-in user.
/// <c>[SessionAuthorize("Organiser")]</c> = only users whose session role is Organiser.
/// Not logged in -> 401 Unauthorized. Logged in with the wrong role -> 403 Forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _roles;

    public SessionAuthorizeAttribute(params string[] roles) => _roles = roles;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Authentication required",
                "You must log in (POST /api/auth/login) before using this endpoint.");
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role, StringComparer.Ordinal))
        {
            context.Result = Problem(StatusCodes.Status403Forbidden, "Forbidden",
                $"This endpoint requires the role: {string.Join(" or ", _roles)}. You are logged in as {role}.");
        }
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
