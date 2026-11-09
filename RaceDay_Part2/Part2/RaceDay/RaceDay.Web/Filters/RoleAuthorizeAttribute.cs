using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Session-based guard. No session -> login. Wrong role -> access denied.
/// Usage: [RoleAuthorize] (any signed-in user) or [RoleAuthorize("Organiser")].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RoleAuthorizeAttribute : ActionFilterAttribute
{
    private readonly string[] _roles;

    public RoleAuthorizeAttribute(params string[] roles) => _roles = roles;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.Session.GetUser();
        if (user == null)
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(user.Role, StringComparer.OrdinalIgnoreCase))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
