using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Registers a new user account.</summary>
    /// <remarks>
    /// Expects fullName, email, password (min 8 chars with a letter and a number) and roleId (1 = Organiser, 2 = Participant).
    /// Anyone can register as a Participant. To stop users from simply promoting themselves, registering as an
    /// Organiser also requires the secret <c>organiserCode</c> issued by the system owner.
    /// Returns 201 with the created user, or 400 for invalid data / a duplicate email / a wrong organiser code.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        if (request.RoleId == RoleNames.OrganiserRoleId)
        {
            var expected = _config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expected) || request.OrganiserCode != expected)
                return BadRequestProblem("A valid organiserCode is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequestProblem("An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordService.Hash(request.Password),
            RoleId = request.RoleId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _db.Entry(user).Reference(u => u.Role).LoadAsync();
        return Created("/api/users/profile", user.ToResponse());
    }

    /// <summary>Logs a user in and starts a session.</summary>
    /// <remarks>
    /// Expects email and password. On success the server stores the user id and role in the session and sends a
    /// session cookie; the browser (or Swagger UI) sends it automatically with every later request.
    /// Returns 200 with the user, or 401 for invalid credentials.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordService.Verify(request.Password, user.PasswordHash))
            return UnauthorizedProblem("Invalid email or password.");

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);
        HttpContext.Session.SetString(SessionKeys.Name, user.FullName);

        return Ok(user.ToResponse());
    }

    /// <summary>Ends the current session (log out).</summary>
    /// <remarks>Requires a logged-in user. Returns 204 and clears the session.</remarks>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}
