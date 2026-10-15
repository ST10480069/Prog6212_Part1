using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>
/// Starts the real API in memory for each test class. The SQL Server database is swapped for EF Core's
/// in-memory database, so the tests need no SQL Server and run the same locally and on GitHub Actions.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";
    private readonly string _databaseName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:OrganiserCode"] = OrganiserCode,
                ["Security:BcryptWorkFactor"] = "4" // cheapest valid cost: keeps the test run fast
            }));

        builder.ConfigureServices(services =>
        {
            var sqlServerOptions = services.Single(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            services.Remove(sqlServerOptions);
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }
}

/// <summary>A registered, logged-in user with their own HttpClient (and therefore their own session cookie).</summary>
public sealed record TestUser(HttpClient Client, UserProfileDto Profile, string Email);

/// <summary>Small helpers so each test reads like the scenario it checks.</summary>
public static class Api
{
    public const string Password = "Password123!";

    /// <summary>A new client with no session (an anonymous visitor). Each client keeps its own cookies.</summary>
    public static HttpClient NewClient(this TestApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static async Task<UserProfileDto> RegisterAsync(this HttpClient client, string role, string? email = null)
    {
        email ??= $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = $"Test {role}",
            email,
            password = Password,
            phoneNumber = "0820000000",
            role,
            organiserCode = role == "Organiser" ? TestApiFactory.OrganiserCode : null
        });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<UserProfileDto>();
    }

    public static async Task LoginAsync(this HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Registers a fresh user with the given role and logs them in.</summary>
    public static async Task<TestUser> NewUserAsync(this TestApiFactory factory, string role)
    {
        var client = factory.NewClient();
        var profile = await client.RegisterAsync(role);
        await client.LoginAsync(profile.Email);
        return new TestUser(client, profile, profile.Email);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>())!;

    public static object EventBody(string name = "Test Event", string type = "Run", string? status = null) => new
    {
        name,
        eventDate = "2027-03-14",
        location = "Pretoria",
        description = "A test event",
        distanceKm = 10m,
        eventType = type,
        status
    };

    public static async Task<EventDto> CreateEventAsync(this HttpClient organiser, string type = "Run", string? status = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody($"Event {Guid.NewGuid():N}", type, status));
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<EventDto>();
    }

    public static async Task<CategoryDto> CreateCategoryAsync(this HttpClient organiser, int eventId, string name = "10km", int maxParticipants = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name, distanceKm = 10m, maxParticipants, price = 100m });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<CategoryDto>();
    }

    public static Task<HttpResponseMessage> EnrolAsync(this HttpClient participant, int categoryId) =>
        participant.PostAsync($"/api/categories/{categoryId}/enrolments", null);

    /// <summary>Organiser + published event + one category, ready for participants to enrol in.</summary>
    public static async Task<(TestUser Organiser, EventDto Event, CategoryDto Category)> SetupEventAsync(
        this TestApiFactory factory, int maxParticipants = 100)
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();
        var category = await organiser.Client.CreateCategoryAsync(ev.EventId, "10km", maxParticipants);
        return (organiser, ev, category);
    }

    /// <summary>Participant enrols in the category and the enrolment is returned.</summary>
    public static async Task<EnrolmentDto> EnrolSuccessfullyAsync(this TestUser participant, int categoryId)
    {
        var response = await participant.Client.EnrolAsync(categoryId);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<EnrolmentDto>();
    }
}
