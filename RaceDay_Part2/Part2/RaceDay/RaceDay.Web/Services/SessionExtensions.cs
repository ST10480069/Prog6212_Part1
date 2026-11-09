using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public static class SessionExtensions
{
    private const string UserKey = "RaceDay.User";

    public static void SetUser(this ISession session, SessionUser user) =>
        session.SetString(UserKey, JsonSerializer.Serialize(user));

    public static SessionUser? GetUser(this ISession session)
    {
        var json = session.GetString(UserKey);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<SessionUser>(json); }
        catch (JsonException) { return null; }
    }
}

public static class Format
{
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Colour class for any status the API can return.</summary>
    public static string StatusClass(string? status) => (status ?? "").ToLowerInvariant() switch
    {
        "confirmed" or "published" or "finished" => "status-confirmed",   // green
        "pending" or "draft" => "status-pending",                         // amber
        "cancelled" or "dnf" or "dq" => "status-cancelled",               // red
        "completed" => "status-completed",                                // blue
        _ => "status-pending"
    };
}
