namespace RaceDay.Api.Models;

/// <summary>Lookup table holding the two roles: Organiser and Participant.</summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

/// <summary>
/// A registered user. Mapped to the [User] table from the Part 1 ERD
/// (the class is called AppUser so it does not clash with ControllerBase.User).
/// </summary>
public class AppUser
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public AppUser Organiser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Headline distance of the event in kilometres (Part 2 addition).</summary>
    public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle (Part 2 addition).</summary>
    public string EventType { get; set; } = EventTypes.Run;
    /// <summary>Draft, Published, Cancelled or Completed.</summary>
    public string Status { get; set; } = EventStatuses.Published;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Distance or Age (Part 2 addition).</summary>
    public string CategoryType { get; set; } = CategoryTypes.Distance;
    /// <summary>Set for Distance categories, null for Age categories.</summary>
    public decimal? DistanceKm { get; set; }
    /// <summary>Set for Age categories (e.g. 50 for "Senior 50+").</summary>
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal Price { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public AppUser Participant { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime EnrolmentDate { get; set; }
    public int BibNumber { get; set; }
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;

    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
    public TimeOnly? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = ResultStatuses.Finished;
}
