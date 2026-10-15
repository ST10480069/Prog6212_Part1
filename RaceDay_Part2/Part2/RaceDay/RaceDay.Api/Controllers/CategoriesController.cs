using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age and distance categories for an event (e.g. Under 20, Senior 50+, 10km, 21km).</summary>
public class CategoriesController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>List the categories of an event. Role: none (public).</summary>
    /// <param name="eventId">The event id.</param>
    /// <returns>The event's categories.</returns>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">No such event.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(int eventId)
    {
        var ev = await db.Events.SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null || await IsHiddenDraftAsync(ev)) return NotFoundProblem($"Event {eventId} does not exist.");

        var categories = await db.Categories.Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId).ToListAsync();
        return Ok(categories.Select(c => c.ToDto()));
    }

    /// <summary>Add a category to an event you own. Role: Organiser (owner only).</summary>
    /// <remarks>
    /// Distance category: send <c>distanceKm</c> (e.g. name "10km", distanceKm 10).
    /// Age category: send <c>minAge</c> and optionally <c>maxAge</c> (e.g. name "Senior 50+", minAge 50).
    /// </remarks>
    /// <param name="eventId">The event id.</param>
    /// <param name="request">name, categoryType (optional), distanceKm or minAge/maxAge, maxParticipants, price.</param>
    /// <returns>The created category.</returns>
    /// <response code="201">Category created.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No such event.</response>
    /// <response code="409">The event already has a category with that name.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(int eventId, CategoryRequest request)
    {
        var ev = await db.Events.SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem($"Event {eventId} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only add categories to events that you own.");

        var (error, type) = ResolveType(request);
        if (error is not null) return error;

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return ConflictProblem($"This event already has a category called '{name}'.");

        var category = new Category { EventId = eventId };
        Apply(category, request, type);

        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, category.ToDto());
    }

    /// <summary>Update a category on an event you own. Role: Organiser (owner only).</summary>
    /// <param name="id">The category id.</param>
    /// <param name="request">name, categoryType (optional), distanceKm or minAge/maxAge, maxParticipants, price.</param>
    /// <returns>The updated category.</returns>
    /// <response code="200">Category updated.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the category belongs to another Organiser's event.</response>
    /// <response code="404">No such category.</response>
    /// <response code="409">Name already used in this event, or capacity lower than the current number of enrolments.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} does not exist.");
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only change categories of events that you own.");

        var (error, type) = ResolveType(request);
        if (error is not null) return error;

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.EventId == category.EventId && c.Name == name && c.CategoryId != id))
            return ConflictProblem($"This event already has a category called '{name}'.");

        if (request.MaxParticipants is not null)
        {
            var confirmed = await db.Enrolments.CountAsync(e => e.CategoryId == id && e.Status == EnrolmentStatuses.Confirmed);
            if (request.MaxParticipants < confirmed)
                return ConflictProblem($"This category already has {confirmed} confirmed enrolments, so maxParticipants cannot be lower than that.");
        }

        Apply(category, request, type);
        await db.SaveChangesAsync();
        return Ok(category.ToDto());
    }

    /// <summary>Delete a category from an event you own. Role: Organiser (owner only).</summary>
    /// <param name="id">The category id.</param>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the category belongs to another Organiser's event.</response>
    /// <response code="404">No such category.</response>
    /// <response code="409">The category has enrolments.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundProblem($"Category {id} does not exist.");
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete categories of events that you own.");

        if (await db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return ConflictProblem("This category already has enrolments and cannot be deleted.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Works out whether this is a Distance or Age category and checks the matching fields are present.</summary>
    private (ActionResult? Error, string Type) ResolveType(CategoryRequest r)
    {
        string? type;
        if (r.CategoryType is not null)
        {
            type = CategoryTypes.Match(r.CategoryType);
            if (type is null) return (Invalid("categoryType", "categoryType must be Distance or Age."), "");
        }
        else if (r.DistanceKm is not null) type = CategoryTypes.Distance;
        else if (r.MinAge is not null) type = CategoryTypes.Age;
        else return (Invalid("categoryType", "Send distanceKm (distance category) or minAge (age category)."), "");

        if (type == CategoryTypes.Distance && r.DistanceKm is null)
            return (Invalid("distanceKm", "distanceKm is required for a Distance category."), "");

        if (type == CategoryTypes.Age)
        {
            if (r.MinAge is null) return (Invalid("minAge", "minAge is required for an Age category."), "");
            if (r.MaxAge is not null && r.MaxAge < r.MinAge)
                return (Invalid("maxAge", "maxAge cannot be lower than minAge."), "");
        }

        return (null, type);
    }

    private static void Apply(Category c, CategoryRequest r, string type)
    {
        c.Name = r.Name.Trim();
        c.CategoryType = type;
        c.DistanceKm = type == CategoryTypes.Distance ? r.DistanceKm : null;
        c.MinAge = type == CategoryTypes.Age ? r.MinAge : null;
        c.MaxAge = type == CategoryTypes.Age ? r.MaxAge : null;
        c.MaxParticipants = r.MaxParticipants ?? (c.MaxParticipants > 0 ? c.MaxParticipants : 100);
        c.Price = r.Price ?? c.Price;
    }
}
