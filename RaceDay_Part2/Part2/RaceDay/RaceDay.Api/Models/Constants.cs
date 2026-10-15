namespace RaceDay.Api.Models;

/// <summary>Helper for matching user-supplied text against a fixed list of allowed values.</summary>
public static class Names
{
    /// <summary>Returns the canonical spelling of <paramref name="value"/> (case-insensitive) or null if not allowed.</summary>
    public static string? Match(string[] allowed, string? value) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
    public static string? Match(string? value) => Names.Match(All, value);
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
    public static string? Match(string? value) => Names.Match(All, value);
}

public static class EventStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
    public static readonly string[] All = { Draft, Published, Cancelled, Completed };
    public static string? Match(string? value) => Names.Match(All, value);
}

public static class CategoryTypes
{
    public const string Distance = "Distance";
    public const string Age = "Age";
    public static readonly string[] All = { Distance, Age };
    public static string? Match(string? value) => Names.Match(All, value);
}

public static class EnrolmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DNF = "DNF";
    public const string DQ = "DQ";
    public static readonly string[] All = { Finished, DNF, DQ };
    public static string? Match(string? value) => Names.Match(All, value);
}
