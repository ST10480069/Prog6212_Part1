using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Details needed to create an account.</summary>
public sealed class RegisterRequest
{
    /// <summary>Full name, e.g. "Thandi Mokoena".</summary>
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Unique email address. Used to log in.</summary>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored only as a BCrypt hash.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Only required when Role is "Organiser". Organiser accounts can create events,
    /// so they are protected by an invite code issued by the RaceDay administrators.
    /// </summary>
    public string? OrganiserCode { get; set; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>Body for creating or updating an event.</summary>
public sealed class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Event date in yyyy-MM-dd format.</summary>
    [Required]
    public DateOnly? EventDate { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Headline distance in kilometres (greater than 0).</summary>
    [Required, Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }

    /// <summary>"Run", "Walk" or "Cycle".</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    /// <summary>"Draft", "Published", "Cancelled" or "Completed". Optional: defaults to Published on create and is unchanged on update.</summary>
    public string? Status { get; set; }
}

/// <summary>Body for creating or updating a category. Give distanceKm for a distance category, or minAge (and optionally maxAge) for an age category.</summary>
public sealed class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Distance" or "Age". Optional: inferred from distanceKm / minAge when omitted.</summary>
    public string? CategoryType { get; set; }

    /// <summary>Required for Distance categories (e.g. 10 for "10km").</summary>
    [Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }

    /// <summary>Required for Age categories (e.g. 50 for "Senior 50+").</summary>
    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    /// <summary>Capacity of the category. Defaults to 100.</summary>
    [Range(1, 100000)]
    public int? MaxParticipants { get; set; }

    /// <summary>Entry fee. Defaults to 0.</summary>
    [Range(0, 100000)]
    public decimal? Price { get; set; }
}

/// <summary>Body for recording or updating a race result.</summary>
public sealed class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss. Required when status is Finished.</summary>
    public TimeOnly? FinishTime { get; set; }

    /// <summary>Finishing position within the category. Required when status is Finished.</summary>
    [Range(1, 100000)]
    public int? Position { get; set; }

    /// <summary>"Finished", "DNF" or "DQ". Defaults to Finished.</summary>
    public string? Status { get; set; }
}
