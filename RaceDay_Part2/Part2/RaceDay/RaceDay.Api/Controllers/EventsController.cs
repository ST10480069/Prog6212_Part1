using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (public) and manage them (Organiser who owns the event).</summary>
public class EventsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>List published events. Role: none (public).</summary>
    /// <remarks>Draft events are hidden. Optionally filter with <c>?eventType=Run|Walk|Cycle</c>.</remarks>
    /// <param name="eventType">Optional filter: Run, Walk or Cycle.</param>
    /// <returns>Events ordered by date.</returns>
    /// <response code="200">Array of events (may be empty).</response>
    /// <response code="400">eventType is not Run, Walk or Cycle.</response>
    [HttpGet("api/events")]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents([FromQuery] string? eventType = null)
    {
        var query = db.Events.Include(e => e.Organiser).Where(e => e.Status != EventStatuses.Draft);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = EventTypes.Match(eventType);
            if (type is null) return Invalid("eventType", "eventType must be Run, Walk or Cycle.");
            query = query.Where(e => e.EventType == type);
        }

        var events = await query.ToListAsync();
        return Ok(events.OrderBy(e => e.EventDate).Select(e => e.ToDto()));
    }

    /// <summary>Get one event. Role: none (public).</summary>
    /// <param name="id">The event id.</param>
    /// <returns>The event details.</returns>
    /// <response code="200">The event.</response>
    /// <response code="404">No such event (or it is a draft owned by someone else).</response>
    [HttpGet("api/events/{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetEvent(int id)
    {
        var ev = await db.Events.Include(e => e.Organiser).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null || await IsHiddenDraftAsync(ev)) return NotFoundProblem($"Event {id} does not exist.");
        return Ok(ev.ToDto());
    }

    /// <summary>Create an event owned by the logged-in Organiser. Role: Organiser.</summary>
    /// <param name="request">name, eventDate (yyyy-MM-dd), location, description, distanceKm, eventType (Run/Walk/Cycle), status (optional, default Published).</param>
    /// <returns>The created event.</returns>
    /// <response code="201">Event created.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not an Organiser.</response>
    [HttpPost("api/events")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> CreateEvent(EventRequest request)
    {
        var type = EventTypes.Match(request.EventType);
        if (type is null) return Invalid("eventType", "eventType must be Run, Walk or Cycle.");

        var status = EventStatuses.Published;
        if (request.Status is not null)
        {
            var matched = EventStatuses.Match(request.Status);
            if (matched is null) return Invalid("status", "status must be Draft, Published, Cancelled or Completed.");
            status = matched;
        }

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            Description = request.Description?.Trim(),
            DistanceKm = request.DistanceKm!.Value,
            EventType = type,
            Status = status
        };

        db.Events.Add(ev);
        await db.SaveChangesAsync();
        await db.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Update an event you own. Role: Organiser (owner only).</summary>
    /// <param name="id">The event id.</param>
    /// <param name="request">All event fields. status is optional (unchanged when omitted).</param>
    /// <returns>The updated event.</returns>
    /// <response code="200">Event updated.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No such event.</response>
    [HttpPut("api/events/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(int id, EventRequest request)
    {
        var ev = await db.Events.Include(e => e.Organiser).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only change events that you own.");

        var type = EventTypes.Match(request.EventType);
        if (type is null) return Invalid("eventType", "eventType must be Run, Walk or Cycle.");

        if (request.Status is not null)
        {
            var status = EventStatuses.Match(request.Status);
            if (status is null) return Invalid("status", "status must be Draft, Published, Cancelled or Completed.");
            ev.Status = status;
        }

        ev.Name = request.Name.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.Description = request.Description?.Trim();
        ev.DistanceKm = request.DistanceKm!.Value;
        ev.EventType = type;

        await db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Delete an event you own. Role: Organiser (owner only).</summary>
    /// <remarks>The event's categories are deleted with it. An event that already has enrolments cannot be deleted; set its status to Cancelled instead.</remarks>
    /// <param name="id">The event id.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No such event.</response>
    /// <response code="409">The event has enrolments.</response>
    [HttpDelete("api/events/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundProblem($"Event {id} does not exist.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete events that you own.");

        if (await db.Enrolments.AnyAsync(e => e.Category.EventId == id))
            return ConflictProblem("This event already has enrolments and cannot be deleted. Set its status to Cancelled instead.");

        db.Categories.RemoveRange(await db.Categories.Where(c => c.EventId == id).ToListAsync());
        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
