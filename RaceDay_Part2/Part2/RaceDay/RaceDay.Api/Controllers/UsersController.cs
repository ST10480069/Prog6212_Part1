using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>The logged-in user's own profile (both roles).</summary>
[Route("api/users/me")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class UsersController(RaceDayDbContext db) : ApiControllerBase
{
    /// <summary>View my profile. Role: any logged-in user.</summary>
    /// <returns>The profile of the user in the current session.</returns>
    /// <response code="200">The profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfileDto>> GetMe()
    {
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Unauthorized();
        return Ok(user.ToDto());
    }

    /// <summary>Update my profile (full name and phone number). Role: any logged-in user.</summary>
    /// <param name="request">fullName (required) and phoneNumber (optional).</param>
    /// <returns>The updated profile.</returns>
    /// <response code="200">Profile updated.</response>
    /// <response code="400">fullName missing or a value is too long.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileRequest request)
    {
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Unauthorized();

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await db.SaveChangesAsync();
        return Ok(user.ToDto());
    }
}
