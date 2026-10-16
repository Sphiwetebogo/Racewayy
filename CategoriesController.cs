using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    /// <summary>Gets the categories of an event (public).</summary>
    /// <remarks>No login needed. Returns 200 with an array of categories (name, distance, fee) or 404 if the event does not exist.</remarks>
    [HttpGet("api/events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryResponse>>> GetByEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFoundProblem("Event not found.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(categories.Select(c => c.ToResponse()).ToList());
    }

    /// <summary>Adds an age or distance category to an event.</summary>
    /// <remarks>
    /// Organiser only, and only for their own events. Expects categoryName (e.g. "Under 20", "10km"), distanceKM and fee.
    /// Returns 201 with the category, 400, 401, 403, 404, or 409 if the event already has a category with that name.
    /// </remarks>
    [HttpPost("api/events/{eventId:int}/categories")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundProblem("Event not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only add categories to events that you created.");

        var name = request.CategoryName.Trim();
        var lower = name.ToLower();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.CategoryName.ToLower() == lower))
            return ConflictProblem("This event already has a category with that name.");

        var category = new Category
        {
            EventId = eventId,
            CategoryName = name,
            DistanceKM = request.DistanceKM,
            Fee = request.Fee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByEvent), new { eventId }, category.ToResponse());
    }
}
