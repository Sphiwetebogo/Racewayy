using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class ResultsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public ResultsController(AppDbContext db) => _db = db;

    /// <summary>Records the finish time and finishing position of a Participant after an event.</summary>
    /// <remarks>
    /// Organiser only, and only for their own events. Expects enrolmentId, finishTimeSeconds and position.
    /// Returns 201 with the result, 400 (invalid data, event not finished yet, or cancelled enrolment), 401, 403,
    /// 404 for an unknown enrolment, or 409 if the enrolment already has a result or the position is already taken.
    /// </remarks>
    [HttpPost("api/results")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Create([FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);

        if (enrolment is null) return NotFoundProblem("Enrolment not found.");

        var ev = enrolment.Category.Event;
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only record results for events that you created.");
        if (ev.EventDate > DateTime.UtcNow) return BadRequestProblem("Results can only be recorded after the event has taken place.");
        if (enrolment.PaymentStatus == PaymentStatus.Cancelled) return BadRequestProblem("This enrolment was cancelled.");

        if (await _db.RaceResults.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId))
            return ConflictProblem("A result has already been recorded for this enrolment.");

        var eventId = ev.EventId;
        if (await _db.RaceResults.AnyAsync(r => r.Enrolment.Category.EventId == eventId && r.Position == request.Position))
            return ConflictProblem("Another participant already has that finishing position in this event.");

        var result = new RaceResult
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTimeSeconds = request.FinishTimeSeconds,
            Position = request.Position
        };

        _db.RaceResults.Add(result);
        await _db.SaveChangesAsync();

        var dto = await _db.RaceResults.AsNoTracking().Where(r => r.ResultId == result.ResultId).ToResponse().SingleAsync();
        return CreatedAtAction(nameof(GetForEvent), new { eventId }, dto);
    }

    /// <summary>Gets the race results of an event (public).</summary>
    /// <remarks>No login needed. Returns 200 with an array of results ordered by position, or 404 if the event does not exist.</remarks>
    [HttpGet("api/events/{eventId:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultResponse>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFoundProblem("Event not found.");

        var list = await _db.RaceResults.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .OrderBy(r => r.Position)
            .ToResponse()
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Gets the logged-in Participant's own race history.</summary>
    /// <remarks>Participant only. Returns 200 with the participant's results (newest first), including position and total finishers; 401 or 403.</remarks>
    [HttpGet("api/results/my")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultResponse>>> GetMine()
    {
        var participantId = CurrentUserId;
        var list = await _db.RaceResults.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToResponse()
            .ToListAsync();
        return Ok(list);
    }
}
