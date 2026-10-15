using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public sealed record UserProfileDto(
    int UserId, string FullName, string Email, string? PhoneNumber, string Role, DateTime CreatedAt);

public sealed record LoginResponse(string Message, UserProfileDto User);

public sealed record EventDto(
    int EventId, int OrganiserId, string OrganiserName, string Name, DateOnly EventDate,
    string Location, string? Description, decimal DistanceKm, string EventType, string Status);

public sealed record CategoryDto(
    int CategoryId, int EventId, string Name, string CategoryType, decimal? DistanceKm,
    int? MinAge, int? MaxAge, int MaxParticipants, decimal Price);

public sealed record EnrolmentDto(
    int EnrolmentId, int EventId, string EventName, int CategoryId, string CategoryName,
    int ParticipantId, string ParticipantName, int BibNumber, DateTime EnrolmentDate, string Status);

public sealed record ResultDto(
    int ResultId, int EnrolmentId, int EventId, string EventName, int CategoryId, string CategoryName,
    string ParticipantName, int BibNumber, TimeOnly? FinishTime, int? Position, string Status);

/// <summary>Entity -> DTO mapping. The entities must be loaded with the navigation properties used below.</summary>
public static class Mappers
{
    public static UserProfileDto ToDto(this AppUser u) =>
        new(u.UserId, u.FullName, u.Email, u.PhoneNumber, u.Role.RoleName, u.CreatedAt);

    public static EventDto ToDto(this Event e) =>
        new(e.EventId, e.OrganiserId, e.Organiser.FullName, e.Name, e.EventDate, e.Location,
            e.Description, e.DistanceKm, e.EventType, e.Status);

    public static CategoryDto ToDto(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.CategoryType, c.DistanceKm, c.MinAge, c.MaxAge,
            c.MaxParticipants, c.Price);

    public static EnrolmentDto ToDto(this Enrolment e) =>
        new(e.EnrolmentId, e.Category.EventId, e.Category.Event.Name, e.CategoryId, e.Category.Name,
            e.ParticipantId, e.Participant.FullName, e.BibNumber, e.EnrolmentDate, e.Status);

    public static ResultDto ToDto(this Result r) =>
        new(r.ResultId, r.EnrolmentId, r.Enrolment.Category.EventId, r.Enrolment.Category.Event.Name,
            r.Enrolment.CategoryId, r.Enrolment.Category.Name, r.Enrolment.Participant.FullName,
            r.Enrolment.BibNumber, r.FinishTime, r.Position, r.Status);
}
