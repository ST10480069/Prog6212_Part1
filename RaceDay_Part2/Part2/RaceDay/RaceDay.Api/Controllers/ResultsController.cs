using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Finish times and positions. Organisers record them; Participants view their own; event results are public.</summary>
public class ResultsController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>Record the result for an enrolment in an event you own. Role: Organiser (owner only).</summary>
    /// <remarks>
    /// Status Finished needs <c>finishTime</c> (hh:mm:ss) and <c>position</c>.
    /// Status DNF or DQ records no time or position. A position can only be used once per category.
    /// </remarks>
    /// <param name="id">The enrolment id.</param>
    /// <param name="request">finishTime, position, status (default Finished).</param>
    /// <returns>The recorded result.</returns>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the enrolment is in another Organiser's event.</response>
    /// <response code="404">No such enrolment.</response>
    /// <response code="409">A result already exists, the enrolment is cancelled, or the position is already taken in this category.</response>
    [HttpPost("api/enrolments/{id:int}/results")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> RecordResult(int id, ResultRequest request)
    {
        var enrolment = await db.Enrolments.WithDetails().Include(e => e.Result)
            .SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFoundProblem($"Enrolment {id} does not exist.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only record results for events that you own.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled)
            return ConflictProblem("This enrolment was cancelled, so no result can be recorded.");
        if (enrolment.Result is not null)
            return ConflictProblem("A result has already been recorded for this enrolment. Use PUT /api/results/{id} to change it.");

        var (error, status, time, position) = Validate(request);
        if (error is not null) return error;

        if (await PositionTaken(enrolment.CategoryId, position, excludeResultId: null))
            return ConflictProblem($"Position {position} is already taken in this category.");

        var result = new Result { EnrolmentId = id, Enrolment = enrolment, FinishTime = time, Position = position, Status = status };
        db.Results.Add(result);
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, result.ToDto());
    }

    /// <summary>Correct a result in an event you own. Role: Organiser (owner only).</summary>
    /// <param name="id">The result id.</param>
    /// <param name="request">finishTime, position, status.</param>
    /// <returns>The updated result.</returns>
    /// <response code="200">Result updated.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the result is in another Organiser's event.</response>
    /// <response code="404">No such result.</response>
    /// <response code="409">The position is already taken in this category.</response>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> UpdateResult(int id, ResultRequest request)
    {
        var result = await db.Results.WithDetails().SingleOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFoundProblem($"Result {id} does not exist.");
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
            return ForbiddenProblem("You can only change results for events that you own.");

        var (error, status, time, position) = Validate(request);
        if (error is not null) return error;

        if (await PositionTaken(result.Enrolment.CategoryId, position, excludeResultId: id))
            return ConflictProblem($"Position {position} is already taken in this category.");

        result.FinishTime = time;
        result.Position = position;
        result.Status = status;
        await db.SaveChangesAsync();
        return Ok(result.ToDto());
    }

    /// <summary>List the results of an event. Role: none (public).</summary>
    /// <param name="eventId">The event id.</param>
    /// <returns>Results grouped by category and ordered by position.</returns>
    /// <response code="200">Array of results (empty until the Organiser records some).</response>
    /// <response code="404">No such event.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetEventResults(int eventId)
    {
        var ev = await db.Events.SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null || await IsHiddenDraftAsync(ev)) return NotFoundProblem($"Event {eventId} does not exist.");

        var results = await db.Results.WithDetails()
            .Where(r => r.Enrolment.Category.EventId == eventId).ToListAsync();

        return Ok(results
            .OrderBy(r => r.Enrolment.Category.Name)
            .ThenBy(r => r.Position ?? int.MaxValue)
            .Select(r => r.ToDto()));
    }

    /// <summary>List my own results across all events. Role: Participant.</summary>
    /// <returns>The logged-in Participant's results.</returns>
    /// <response code="200">Array of results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not a Participant.</response>
    [HttpGet("api/users/me/results")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> GetMyResults()
    {
        var userId = CurrentUserId;
        var results = await db.Results.WithDetails().Where(r => r.Enrolment.ParticipantId == userId).ToListAsync();
        return Ok(results.OrderBy(r => r.Enrolment.Category.Event.EventDate).Select(r => r.ToDto()));
    }

    private (ActionResult? Error, string Status, TimeOnly? Time, int? Position) Validate(ResultRequest r)
    {
        var status = ResultStatuses.Finished;
        if (r.Status is not null)
        {
            var matched = ResultStatuses.Match(r.Status);
            if (matched is null) return (Invalid("status", "status must be Finished, DNF or DQ."), "", null, null);
            status = matched;
        }

        if (status != ResultStatuses.Finished)
            return (null, status, null, null); // DNF / DQ have no time or position

        if (r.FinishTime is null) return (Invalid("finishTime", "finishTime is required when status is Finished."), "", null, null);
        if (r.Position is null) return (Invalid("position", "position is required when status is Finished."), "", null, null);
        return (null, status, r.FinishTime, r.Position);
    }

    private Task<bool> PositionTaken(int categoryId, int? position, int? excludeResultId) =>
        position is null
            ? Task.FromResult(false)
            : db.Results.AnyAsync(r => r.Enrolment.CategoryId == categoryId
                                       && r.Position == position
                                       && (excludeResultId == null || r.ResultId != excludeResultId));
}
