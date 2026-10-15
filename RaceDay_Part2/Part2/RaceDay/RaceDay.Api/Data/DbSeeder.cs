using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Adds the sample data from the Part 1 SQL script so the API has something to show when you run it
/// in Development. Only runs when the User table is empty. Every seeded account has the password "Password123!".
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(RaceDayDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var organiser = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Organiser);
        var participant = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.Participant);
        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        var now = DateTime.UtcNow;

        AppUser U(Role role, string name, string email, string phone) =>
            new() { Role = role, FullName = name, Email = email, PhoneNumber = phone, PasswordHash = hash, CreatedAt = now };

        var john = U(organiser, "John Smith", "john.smith@raceday.com", "0821234567");
        var sarah = U(organiser, "Sarah Lee", "sarah.lee@raceday.com", "0827654321");
        var mike = U(participant, "Mike Johnson", "mike.johnson@example.com", "0731122334");
        var emma = U(participant, "Emma Brown", "emma.brown@example.com", "0739988776");
        db.Users.AddRange(john, sarah, mike, emma);

        var marathon = new Event
        {
            Organiser = john, Name = "City Marathon 2026", EventDate = new DateOnly(2026, 11, 8),
            Location = "Johannesburg CBD", DistanceKm = 21.10m, EventType = EventTypes.Run,
            Description = "An annual road race through the heart of the city, with routes for every ability.",
            Status = EventStatuses.Published
        };
        var trail = new Event
        {
            Organiser = sarah, Name = "Trail Run Challenge", EventDate = new DateOnly(2026, 9, 20),
            Location = "Magaliesberg Trail Park", DistanceKm = 21.00m, EventType = EventTypes.Run,
            Description = "An off-road trail event across scenic mountain terrain.",
            Status = EventStatuses.Published
        };
        var fun = new Event
        {
            Organiser = john, Name = "Fun Run for Charity", EventDate = new DateOnly(2026, 10, 5),
            Location = "Benoni Lake", DistanceKm = 5.00m, EventType = EventTypes.Run,
            Description = "A family-friendly fun run raising funds for local charities.",
            Status = EventStatuses.Published
        };
        db.Events.AddRange(marathon, trail, fun);

        Category D(Event e, string name, decimal km, int max, decimal price) =>
            new() { Event = e, Name = name, CategoryType = CategoryTypes.Distance, DistanceKm = km, MaxParticipants = max, Price = price };

        var m5 = D(marathon, "5km", 5.00m, 500, 150m);
        var m10 = D(marathon, "10km", 10.00m, 500, 200m);
        var mHalf = D(marathon, "Half Marathon", 21.10m, 300, 350m);
        var mUnder20 = new Category
        {
            Event = marathon, Name = "Under 20", CategoryType = CategoryTypes.Age,
            MinAge = 0, MaxAge = 19, MaxParticipants = 200, Price = 100m
        };
        var t10 = D(trail, "10km Trail", 10.00m, 200, 180m);
        var t21 = D(trail, "21km Trail", 21.00m, 150, 300m);
        var f5 = D(fun, "5km Fun Run", 5.00m, 400, 100m);
        var f2 = D(fun, "2km Kids Run", 2.00m, 200, 50m);
        db.Categories.AddRange(m5, m10, mHalf, mUnder20, t10, t21, f5, f2);

        Enrolment E(AppUser who, Category c, int bib) =>
            new() { Participant = who, Category = c, BibNumber = bib, Status = EnrolmentStatuses.Confirmed, EnrolmentDate = now };

        var e1 = E(mike, m5, 1001);
        var e2 = E(emma, m10, 1002);
        var e3 = E(mike, t10, 1001);
        var e4 = E(emma, f5, 1001);
        db.Enrolments.AddRange(e1, e2, e3, e4);

        db.Results.AddRange(
            new Result { Enrolment = e1, FinishTime = new TimeOnly(0, 24, 37), Position = 1, Status = ResultStatuses.Finished },
            new Result { Enrolment = e2, FinishTime = new TimeOnly(0, 52, 14), Position = 1, Status = ResultStatuses.Finished });

        await db.SaveChangesAsync();
    }
}
