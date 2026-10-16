using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db) => _db = db;

    /// <summary>Lists all race events (public).</summary>
    /// <remarks>
    /// No login needed. Optional filters: <c>search</c> (title or location), <c>eventType</c> (Run, Walk, Cycle)
    /// and <c>upcomingOnly</c> (true hides events that already took place). Returns 200 with an array of events
    /// that include their categories and enrolment count, ordered by date.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetAll(
        [FromQuery] string? search, [FromQuery] EventType? eventType, [FromQuery] bool upcomingOnly = false)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(s) || e.Location.ToLower().Contains(s));
        }

        if (eventType.HasValue)
            query = query.Where(e => e.EventType == eventType.Value);

        if (upcomingOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(e => e.EventDate >= now);
        }

        return Ok(await query.OrderBy(e => e.EventDate).ToResponse().ToListAsync());
    }

    /// <summary>Gets the details of one event (public).</summary>
    /// <remarks>No login needed. Returns 200 with the event and its categories, or 404 if it does not exist.</remarks>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        var dto = await _db.Events.AsNoTracking().Where(e => e.EventId == id).ToResponse().FirstOrDefaultAsync();
        return dto is null ? NotFoundProblem("Event not found.") : Ok(dto);
    }

    /// <summary>Creates a new race event.</summary>
    /// <remarks>
    /// Organiser only. Expects title, description, eventDate, location, distanceKM and eventType (Run, Walk or Cycle).
    /// Returns 201 with the event, 400 for invalid data, 401 if not logged in, or 403 for a Participant.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create([FromBody] EventRequest request)
    {
        var entity = new Event
        {
            OrganiserId = CurrentUserId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKM = request.DistanceKM,
            EventType = request.EventType!.Value
        };

        _db.Events.Add(entity);
        await _db.SaveChangesAsync();

        var dto = await _db.Events.AsNoTracking().Where(e => e.EventId == entity.EventId).ToResponse().SingleAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.EventId }, dto);
    }

    /// <summary>Updates an existing race event.</summary>
    /// <remarks>
    /// Organiser only, and only for events they created. Expects the same body as creating an event.
    /// Returns 200 with the updated event, 400, 401, 403 (Participant or someone else's event) or 404.
    /// </remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, [FromBody] EventRequest request)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (entity is null) return NotFoundProblem("Event not found.");
        if (entity.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only change events that you created.");

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim();
        entity.EventDate = request.EventDate!.Value;
        entity.Location = request.Location.Trim();
        entity.DistanceKM = request.DistanceKM;
        entity.EventType = request.EventType!.Value;
        await _db.SaveChangesAsync();

        var dto = await _db.Events.AsNoTracking().Where(e => e.EventId == id).ToResponse().SingleAsync();
        return Ok(dto);
    }

    /// <summary>Removes a race event together with its categories, enrolments and results.</summary>
    /// <remarks>Organiser only, and only for their own events. Returns 204, 401, 403 or 404.</remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (entity is null) return NotFoundProblem("Event not found.");
        if (entity.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only delete events that you created.");

        foreach (var category in entity.Categories)
        {
            foreach (var enrolment in category.Enrolments)
            {
                if (enrolment.Result is not null) _db.RaceResults.Remove(enrolment.Result);
                _db.Enrolments.Remove(enrolment);
            }
        }

        _db.Categories.RemoveRange(entity.Categories);
        _db.Events.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
