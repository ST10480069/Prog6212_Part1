using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Age and distance categories.</summary>
public class CategoryTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task GetCategories_Anonymous_ReturnsTheEventsCategories()
    {
        var (_, ev, category) = await factory.SetupEventAsync();

        var response = await factory.NewClient().GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.ReadAsync<List<CategoryDto>>();
        Assert.Contains(list, c => c.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task GetCategories_VisibleToParticipants()
    {
        var (_, ev, _) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_UnknownEvent_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDistanceCategory_AsOwner_Returns201()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "21km", distanceKm = 21.1m, maxParticipants = 300, price = 350m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.ReadAsync<CategoryDto>();
        Assert.Equal("Distance", category.CategoryType);
        Assert.Equal(21.1m, category.DistanceKm);
        Assert.Equal(300, category.MaxParticipants);
        Assert.Equal(350m, category.Price);
        Assert.Equal(ev.EventId, category.EventId);
    }

    [Fact]
    public async Task CreateAgeCategory_AsOwner_Returns201()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Senior 50+", categoryType = "Age", minAge = 50 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.ReadAsync<CategoryDto>();
        Assert.Equal("Age", category.CategoryType);
        Assert.Equal(50, category.MinAge);
        Assert.Null(category.MaxAge);
        Assert.Null(category.DistanceKm);
        Assert.Equal(100, category.MaxParticipants); // default
    }

    [Fact]
    public async Task CreateAgeCategory_WithoutMinAge_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Broken", categoryType = "Age" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAgeCategory_MaxAgeBelowMinAge_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Backwards", minAge = 40, maxAge = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithNeitherDistanceNorAge_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Empty" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameInSameEvent_Returns409()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();
        await organiser.Client.CreateCategoryAsync(ev.EventId, "5km");

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "5km", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_SameNameInDifferentEvents_IsAllowed()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var first = await organiser.Client.CreateEventAsync();
        var second = await organiser.Client.CreateEventAsync();
        await organiser.Client.CreateCategoryAsync(first.EventId, "5km");

        await organiser.Client.CreateCategoryAsync(second.EventId, "5km"); // asserts 201 internally
    }

    [Fact]
    public async Task CreateCategory_AsParticipant_Returns403()
    {
        var (_, ev, _) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Sneaky", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_ForAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, _) = await factory.SetupEventAsync();
        var otherOrganiser = await factory.NewUserAsync("Organiser");

        var response = await otherOrganiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Sneaky", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_Anonymous_Returns401()
    {
        var (_, ev, _) = await factory.SetupEventAsync();

        var response = await factory.NewClient().PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Sneaky", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_UnknownEvent_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events/999999/categories", new { name = "X", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_ByOwner_Returns200()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();

        var response = await organiser.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = "10km Elite", distanceKm = 10m, maxParticipants = 50, price = 250m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<CategoryDto>();
        Assert.Equal("10km Elite", updated.Name);
        Assert.Equal(50, updated.MaxParticipants);
        Assert.Equal(250m, updated.Price);
    }

    [Fact]
    public async Task UpdateCategory_ByAnotherOrganiser_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var other = await factory.NewUserAsync("Organiser");

        var response = await other.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = "Hijacked", distanceKm = 10m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_ByParticipant_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = "Hijacked", distanceKm = 10m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_Unknown_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PutAsJsonAsync("/api/categories/999999", new { name = "X", distanceKm = 5m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_CapacityBelowConfirmedEnrolments_Returns409()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();
        await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);
        await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);

        var response = await organiser.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            new { name = category.Name, distanceKm = 10m, maxParticipants = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ByOwner_Returns204()
    {
        var (organiser, ev, category) = await factory.SetupEventAsync();

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var remaining = await (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}/categories")).ReadAsync<List<CategoryDto>>();
        Assert.DoesNotContain(remaining, c => c.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task DeleteCategory_ByAnotherOrganiser_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var other = await factory.NewUserAsync("Organiser");

        var response = await other.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ByParticipant_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_Unknown_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.DeleteAsync("/api/categories/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();
        await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
