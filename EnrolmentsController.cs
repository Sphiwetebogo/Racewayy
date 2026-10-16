using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class EnrolmentsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public EnrolmentsController(AppDbContext db) => _db = db;

    /// <summary>Enrols the logged-in Participant for an event by choosing one of its categories.</summary>
    /// <remarks>
    /// Participant only. Expects categoryId. The link between participant, event and category is recorded immediately
    /// with a payment status of Pending. Returns 201 with the enrolment, 400 if the event already took place,
    /// 401, 403 for an Organiser, 404 for an unknown category, or 409 if the participant is already enrolled for the event.
    /// </remarks>
    [HttpPost("api/enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol([FromBody] EnrolRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null) return NotFoundProblem("Category not found.");

        if (category.Event.EventDate <= DateTime.UtcNow)
            return BadRequestProblem("Enrolment is closed because this event has already taken place.");

        var participantId = CurrentUserId;
        var existing = await _db.Enrolments
            .FirstOrDefaultAsync(e => e.ParticipantId == participantId && e.Category.EventId == category.EventId);

        Enrolment enrolment;
        if (existing is not null)
        {
            if (existing.PaymentStatus != PaymentStatus.Cancelled)
                return ConflictProblem("You are already enrolled for this event.");

            // Re-enrol after a cancellation: reuse the row so the unique constraint is respected.
            existing.Category = category;
            existing.PaymentStatus = PaymentStatus.Pending;
            existing.EnrolmentDate = DateTime.UtcNow;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment { ParticipantId = participantId, CategoryId = category.CategoryId };
            _db.Enrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();

        var dto = await _db.Enrolments.AsNoTracking().Where(e => e.EnrolmentId == enrolment.EnrolmentId).ToResponse().SingleAsync();
        return CreatedAtAction(nameof(GetMine), null, dto);
    }

    /// <summary>Gets all enrolments of the logged-in Participant.</summary>
    /// <remarks>Participant only. Returns 200 with an array of enrolments (event, category, payment status), 401 or 403.</remarks>
    [HttpGet("api/enrolments/my")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetMine()
    {
        var participantId = CurrentUserId;
        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == participantId)
            .OrderBy(e => e.Category.Event.EventDate)
            .ToResponse()
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Cancels one of the logged-in Participant's enrolments.</summary>
    /// <remarks>
    /// Participant only. Sets the payment status to Cancelled. Returns 204, 400 if the event already took place,
    /// 401, 403, or 404 if the enrolment does not exist or belongs to someone else.
    /// </remarks>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var participantId = CurrentUserId;
        var enrolment = await _db.Enrolments.Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id && e.ParticipantId == participantId);

        if (enrolment is null) return NotFoundProblem("Enrolment not found.");
        if (enrolment.Category.Event.EventDate <= DateTime.UtcNow)
            return BadRequestProblem("You cannot cancel an enrolment for an event that has already taken place.");

        enrolment.PaymentStatus = PaymentStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Lists everyone enrolled for one of the Organiser's events.</summary>
    /// <remarks>
    /// Organiser only, and only for their own events. Returns 200 with an array of enrolments including the
    /// participant, selected category and payment status; 401, 403 or 404.
    /// </remarks>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only view enrolments for events that you created.");

        var list = await _db.Enrolments.AsNoTracking()
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Participant.FullName)
            .ToResponse()
            .ToListAsync();
        return Ok(list);
    }
}
