using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The logged-in user's id. Only call this on actions protected by [SessionAuthorize].</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)
        ?? throw new InvalidOperationException("No user in session.");

    /// <summary>Returns the logged-in user's id, or null on public endpoints when nobody is logged in.</summary>
    protected async Task<int?> TryGetUserIdAsync()
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);
        return HttpContext.Session.GetInt32(SessionKeys.UserId);
    }

    /// <summary>400 response that names the offending field.</summary>
    protected ActionResult Invalid(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail);

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);

    /// <summary>Draft events are only visible to the Organiser who owns them.</summary>
    protected async Task<bool> IsHiddenDraftAsync(Event ev)
    {
        if (ev.Status != EventStatuses.Draft) return false;
        var userId = await TryGetUserIdAsync();
        return userId != ev.OrganiserId;
    }
}

/// <summary>Query helpers that load everything the DTO mappers need.</summary>
public static class QueryExtensions
{
    public static IQueryable<Enrolment> WithDetails(this IQueryable<Enrolment> q) =>
        q.Include(e => e.Participant).Include(e => e.Category).ThenInclude(c => c.Event);

    public static IQueryable<Result> WithDetails(this IQueryable<Result> q) =>
        q.Include(r => r.Enrolment).ThenInclude(e => e.Participant)
         .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event);
}
