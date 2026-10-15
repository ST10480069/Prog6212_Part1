using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enrol in an event by choosing a category; Organisers view who has enrolled.</summary>
public class EnrolmentsController(RaceDayDbContext db) : ApiControllerBase
{
    private const int FirstBibNumber = 1001;

    /// <summary>Enrol the logged-in Participant in a category. Role: Participant.</summary>
    /// <remarks>
    /// No request body is needed. The participant comes from the session, the event comes from the category,
    /// and the bib number is assigned by the server, so nobody can enrol as someone else or pick their own bib.
    /// A participant can hold only one confirmed enrolment per event.
    /// </remarks>
    /// <param name="categoryId">The category to enter.</param>
    /// <returns>The enrolment, including the assigned bib number.</returns>
    /// <response code="201">Enrolled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    /// <response code="404">No such category.</response>
    /// <response code="409">Already enrolled in this event, the category is full, or the event is not open for enrolment.</response>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int categoryId)
    {
        var category = await db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null || category.Event.Status == EventStatuses.Draft)
            return NotFoundProblem($"Category {categoryId} does not exist.");

        if (category.Event.Status != EventStatuses.Published)
            return ConflictProblem($"This event is {category.Event.Status} and is not open for enrolment.");

        var userId = CurrentUserId;
        var eventId = category.EventId;

        var existing = await db.Enrolments.Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.ParticipantId == userId && e.Category.EventId == eventId);
        if (existing is { Status: EnrolmentStatuses.Confirmed })
            return ConflictProblem("You are already enrolled in this event.");

        var confirmed = await db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status == EnrolmentStatuses.Confirmed);
        if (confirmed >= category.MaxParticipants)
            return ConflictProblem("This category is full.");

        // Bib numbers are unique within an event and start at 1001.
        var highestBib = await db.Enrolments.Where(e => e.Category.EventId == eventId).MaxAsync(e => (int?)e.BibNumber);
        var bib = (highestBib ?? FirstBibNumber - 1) + 1;

        Enrolment enrolment;
        if (existing is not null)
        {
            // The participant cancelled earlier and is coming back: reuse the cancelled record.
            enrolment = existing;
            enrolment.CategoryId = categoryId;
            enrolment.BibNumber = bib;
            enrolment.Status = EnrolmentStatuses.Confirmed;
            enrolment.EnrolmentDate = DateTime.UtcNow;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = userId,
                CategoryId = categoryId,
                BibNumber = bib,
                Status = EnrolmentStatuses.Confirmed,
                EnrolmentDate = DateTime.UtcNow
            };
            db.Enrolments.Add(enrolment);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("The enrolment could not be completed (someone else enrolled at the same moment). Please try again.");
        }

        var saved = await db.Enrolments.WithDetails().SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        return StatusCode(StatusCodes.Status201Created, saved.ToDto());
    }

    /// <summary>List my own enrolments. Role: Participant.</summary>
    /// <returns>All enrolments (confirmed and cancelled) of the logged-in Participant, newest first.</returns>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    [HttpGet("api/users/me/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetMyEnrolments()
    {
        var userId = CurrentUserId;
        var list = await db.Enrolments.WithDetails().Where(e => e.ParticipantId == userId).ToListAsync();
        return Ok(list.OrderByDescending(e => e.EnrolmentDate).Select(e => e.ToDto()));
    }

    /// <summary>List everyone enrolled in a category of an event you own. Role: Organiser (owner only).</summary>
    /// <param name="categoryId">The category id.</param>
    /// <returns>The enrolments of that category ordered by bib number.</returns>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the category belongs to another Organiser's event.</response>
    /// <response code="404">No such category.</response>
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> GetCategoryEnrolments(int categoryId)
    {
        var category = await db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundProblem($"Category {categoryId} does not exist.");
        if (category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only view enrolments for events that you own.");

        var list = await db.Enrolments.WithDetails().Where(e => e.CategoryId == categoryId).ToListAsync();
        return Ok(list.OrderBy(e => e.BibNumber).Select(e => e.ToDto()));
    }

    /// <summary>Cancel my own enrolment. Role: Participant (own enrolments only).</summary>
    /// <remarks>The record is kept with status Cancelled and the place is freed. Cancelling twice is harmless.</remarks>
    /// <param name="id">The enrolment id.</param>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or the enrolment belongs to someone else.</response>
    /// <response code="404">No such enrolment.</response>
    /// <response code="409">A result has already been recorded for this enrolment.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await db.Enrolments.Include(e => e.Result).SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundProblem($"Enrolment {id} does not exist.");
        if (enrolment.ParticipantId != CurrentUserId) return ForbiddenProblem("You can only cancel your own enrolments.");
        if (enrolment.Result is not null) return ConflictProblem("A result has already been recorded, so this enrolment cannot be cancelled.");

        if (enrolment.Status != EnrolmentStatuses.Cancelled)
        {
            enrolment.Status = EnrolmentStatuses.Cancelled;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}
