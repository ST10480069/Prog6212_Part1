using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

// ---- Session ----
public class SessionUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";   // "Organiser" | "Participant"
}

// ---- API response shapes (match Part 2 Dtos/Responses.cs) ----
public class UserProfileDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

public class LoginResponseDto
{
    public string Message { get; set; } = "";
    public UserProfileDto User { get; set; } = new();
}

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = "";
    public string? Description { get; set; }
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BannerUrl { get; set; }

    // Filled in by the MVC controllers (not part of the API response)
    public int EnrolmentCount { get; set; }
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string CategoryType { get; set; } = "";
    public decimal? DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }

    public string Detail => CategoryType == "Age"
        ? (MaxAge.HasValue ? $"Ages {MinAge}-{MaxAge}" : $"Ages {MinAge}+")
        : $"{DistanceKm:0.##} km";
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";
    public int BibNumber { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = "";

    public DateOnly? EventDate { get; set; }   // filled in by the controller
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string ParticipantName { get; set; } = "";
    public int BibNumber { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "";

    public DateOnly? EventDate { get; set; }   // filled in by the controller
    public int TotalFinishers { get; set; }    // filled in by the controller
}

public class UploadResultDto
{
    public string Url { get; set; } = "";
}

// ---- Form / view models ----
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required"), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required"), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Email is required"), EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";

    [Phone(ErrorMessage = "Enter a valid phone number"), StringLength(20)]
    [Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Password is required"), StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password"), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Choose a role")]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser code")]
    public string? OrganiserCode { get; set; }
}

public class EventFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Event name is required"), StringLength(150)]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Date is required"), DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddDays(30);

    [Required(ErrorMessage = "Location is required"), StringLength(200)]
    public string Location { get; set; } = "";

    [Required(ErrorMessage = "Distance is required"), Range(0.01, 1000, ErrorMessage = "Distance must be between 0.01 and 1000 km")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Event type is required")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    public string Status { get; set; } = "Published";

    public string? ExistingBannerUrl { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Category name is required"), StringLength(100)]
    public string Name { get; set; } = "";

    [Required]
    [Display(Name = "Category type")]
    public string CategoryType { get; set; } = "Distance";

    [Range(0.01, 1000, ErrorMessage = "Distance must be between 0.01 and 1000 km")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120)]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(1, 100000, ErrorMessage = "Capacity must be at least 1")]
    [Display(Name = "Max participants")]
    public int MaxParticipants { get; set; } = 100;

    [Range(0, 100000, ErrorMessage = "Price cannot be negative")]
    [Display(Name = "Entry fee (R)")]
    public decimal Price { get; set; }
}

public class CategoriesPageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public EventDto? NextEvent => Events
        .Where(e => e.EventDate >= DateOnly.FromDateTime(DateTime.Today))
        .OrderBy(e => e.EventDate).FirstOrDefault();
}

public class EnrolmentsPageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultEntryRow
{
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string ParticipantName { get; set; } = "";
    public int BibNumber { get; set; }
    public string CategoryName { get; set; } = "";
    public string Status { get; set; } = "Finished";   // Finished | DNF | DQ
    public string? FinishTime { get; set; }            // hh:mm:ss
    public int? Position { get; set; }
}

public class ResultsEntryViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public List<ResultEntryRow> Rows { get; set; } = new();
}

public class EnrolFormModel
{
    public int EventId { get; set; }
    public int CategoryId { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public bool CanEnrol { get; set; }
    public bool AlreadyEnrolled { get; set; }
}

public class EventFilter
{
    public string? Search { get; set; }
    public string? EventType { get; set; }
    public string? Location { get; set; }
    public decimal? MaxDistance { get; set; }
}

public class HomeViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public EventFilter Filter { get; set; } = new();
    public string[] EventTypes { get; set; } = { "Run", "Walk", "Cycle" };
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Full name is required"), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    [Phone(ErrorMessage = "Enter a valid phone number"), StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    public string? ProfilePictureUrl { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? Picture { get; set; }
}
