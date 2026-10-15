using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login starts a server-side session that stores the user's id and role.</summary>
[Route("api/auth")]
public class AuthController(RaceDayDbContext db, IConfiguration config) : ApiControllerBase
{
    /// <summary>Register a new Organiser or Participant account.</summary>
    /// <remarks>
    /// Anyone can register as a **Participant**. Registering as an **Organiser** also needs the
    /// organiser invite code (<c>organiserCode</c>), so random visitors cannot give themselves
    /// the power to create events and record results. The password is hashed with BCrypt before it is stored.
    /// </remarks>
    /// <param name="request">fullName, email, password (min 8 chars), phoneNumber (optional), role, organiserCode (Organiser only).</param>
    /// <returns>The created user profile (never includes the password).</returns>
    /// <response code="201">Account created.</response>
    /// <response code="400">A field is missing or invalid (e.g. role is not Organiser/Participant, password too short).</response>
    /// <response code="403">Registering as Organiser without a valid organiser code.</response>
    /// <response code="409">That email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileDto>> Register(RegisterRequest request)
    {
        var roleName = RoleNames.Match(request.Role);
        if (roleName is null)
            return Invalid("role", "Role must be either 'Organiser' or 'Participant'.");

        if (roleName == RoleNames.Organiser)
        {
            var expected = config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expected) || !CodesMatch(request.OrganiserCode, expected))
                return ForbiddenProblem("A valid organiserCode is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return ConflictProblem("That email address is already registered.");

        var role = await db.Roles.SingleAsync(r => r.RoleName == roleName);
        var user = new AppUser
        {
            RoleId = role.RoleId,
            Role = role,
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            // Work factor 11 by default; the unit tests lower it (Security:BcryptWorkFactor) so they run fast.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password,
                config.GetValue<int?>("Security:BcryptWorkFactor") ?? 11),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two requests registered the same email at the same moment: the unique index caught it.
            return ConflictProblem("That email address is already registered.");
        }

        return StatusCode(StatusCodes.Status201Created, user.ToDto());
    }

    /// <summary>Log in with email and password and start a session.</summary>
    /// <remarks>
    /// On success the server sets a session cookie. The browser (including Swagger UI) sends it automatically on
    /// later requests, and the API uses it to know who you are and which role you have.
    /// </remarks>
    /// <param name="request">email and password.</param>
    /// <returns>A confirmation message and the logged-in user's profile.</returns>
    /// <response code="200">Logged in. Session cookie issued.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        // Same message for "no such email" and "wrong password" so attackers cannot probe which emails exist.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Invalid email or password.");

        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);
        await HttpContext.Session.CommitAsync(HttpContext.RequestAborted);

        return Ok(new LoginResponse("Logged in. A session cookie has been issued.", user.ToDto()));
    }

    /// <summary>Log out and end the current session.</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    /// <summary>Constant-time comparison so response timing does not leak the code.</summary>
    private static bool CodesMatch(string? supplied, string expected)
    {
        if (string.IsNullOrEmpty(supplied)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
