using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event management: Organisers manage their events, everyone else is rejected.</summary>
public class EventTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    // ---------------- Public reads ----------------

    [Fact]
    public async Task GetEvents_Anonymous_Returns200()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await factory.NewClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.ReadAsync<List<EventDto>>();
        Assert.Contains(events, e => e.EventId == ev.EventId);
    }

    [Fact]
    public async Task GetEvents_HidesDraftEvents()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var draft = await organiser.Client.CreateEventAsync(status: "Draft");

        var events = await (await factory.NewClient().GetAsync("/api/events")).ReadAsync<List<EventDto>>();

        Assert.DoesNotContain(events, e => e.EventId == draft.EventId);
    }

    [Fact]
    public async Task GetEvents_CanFilterByEventType()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var cycle = await organiser.Client.CreateEventAsync("Cycle");
        var walk = await organiser.Client.CreateEventAsync("Walk");

        var cycles = await (await factory.NewClient().GetAsync("/api/events?eventType=Cycle")).ReadAsync<List<EventDto>>();

        Assert.Contains(cycles, e => e.EventId == cycle.EventId);
        Assert.DoesNotContain(cycles, e => e.EventId == walk.EventId);
        Assert.All(cycles, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task GetEvents_InvalidEventType_Returns400()
    {
        var response = await factory.NewClient().GetAsync("/api/events?eventType=Swim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_Anonymous_ReturnsAllFields()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var created = await organiser.Client.CreateEventAsync("Walk");

        var response = await factory.NewClient().GetAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ev = await response.ReadAsync<EventDto>();
        Assert.Equal(created.Name, ev.Name);
        Assert.Equal("Walk", ev.EventType);
        Assert.Equal(10m, ev.DistanceKm);
        Assert.Equal("Pretoria", ev.Location);
        Assert.Equal("A test event", ev.Description);
        Assert.Equal(new DateOnly(2027, 3, 14), ev.EventDate);
        Assert.Equal(organiser.Profile.UserId, ev.OrganiserId);
    }

    [Fact]
    public async Task GetEvent_Unknown_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_Draft_IsHiddenFromOthers_ButVisibleToItsOwner()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var draft = await organiser.Client.CreateEventAsync(status: "Draft");

        var asVisitor = await factory.NewClient().GetAsync($"/api/events/{draft.EventId}");
        var asOwner = await organiser.Client.GetAsync($"/api/events/{draft.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, asVisitor.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
    }

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201_AndEventCanBeRetrieved()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", Api.EventBody("Soweto Fun Run", "Run"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.ReadAsync<EventDto>();
        Assert.Equal("Soweto Fun Run", created.Name);
        Assert.Equal(organiser.Profile.UserId, created.OrganiserId);
        Assert.Equal("Published", created.Status);

        var fetched = await factory.NewClient().GetAsync($"/api/events/{created.EventId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Anonymous_Returns401()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/events", Api.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_Returns403()
    {
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.PostAsJsonAsync("/api/events", Api.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", Api.EventBody(type: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidStatus_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", Api.EventBody(status: "Maybe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingRequiredFields_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", new { name = "Only a name" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ZeroDistance_Returns400()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/events", new
        {
            name = "Bad", eventDate = "2027-03-14", location = "X", description = "d", distanceKm = 0, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateEvent_ByOwner_Returns200_AndChangesAreSaved()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}",
            Api.EventBody("Renamed Event", "Cycle", "Cancelled"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<EventDto>();
        Assert.Equal("Renamed Event", updated.Name);
        Assert.Equal("Cycle", updated.EventType);
        Assert.Equal("Cancelled", updated.Status);

        var fetched = await (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}")).ReadAsync<EventDto>();
        Assert.Equal("Renamed Event", fetched.Name);
    }

    [Fact]
    public async Task UpdateEvent_WithoutStatus_KeepsExistingStatus()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync(status: "Draft");

        var updated = await (await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody("Same status"))).ReadAsync<EventDto>();

        Assert.Equal("Draft", updated.Status);
    }

    [Fact]
    public async Task UpdateEvent_ByAnotherOrganiser_Returns403()
    {
        var owner = await factory.NewUserAsync("Organiser");
        var other = await factory.NewUserAsync("Organiser");
        var ev = await owner.Client.CreateEventAsync();

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var unchanged = await (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}")).ReadAsync<EventDto>();
        Assert.Equal(ev.Name, unchanged.Name);
    }

    [Fact]
    public async Task UpdateEvent_ByParticipant_Returns403()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var participant = await factory.NewUserAsync("Participant");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await participant.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_Anonymous_Returns401()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await factory.NewClient().PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_Unknown_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PutAsJsonAsync("/api/events/999999", Api.EventBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteEvent_ByOwner_Returns204_AndEventIsGone()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();
        await organiser.Client.CreateCategoryAsync(ev.EventId);

        var response = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_ByAnotherOrganiser_Returns403_AndEventSurvives()
    {
        var owner = await factory.NewUserAsync("Organiser");
        var other = await factory.NewUserAsync("Organiser");
        var ev = await owner.Client.CreateEventAsync();

        var response = await other.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_ByParticipant_Returns403()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var participant = await factory.NewUserAsync("Participant");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await participant.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_Anonymous_Returns401()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var ev = await organiser.Client.CreateEventAsync();

        var response = await factory.NewClient().DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_Unknown_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.DeleteAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_Returns409()
    {
        var (organiser, ev, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        await participant.EnrolSuccessfullyAsync(category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
