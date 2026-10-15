using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Event enrolments: Participants enrol and the enrolment is recorded correctly; other roles are rejected.</summary>
public class EnrolmentTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task Enrol_AsParticipant_Returns201_WithBibNumber()
    {
        var (_, ev, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.ReadAsync<EnrolmentDto>();
        Assert.Equal(participant.Profile.UserId, enrolment.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal("Confirmed", enrolment.Status);
        Assert.Equal(1001, enrolment.BibNumber);
    }

    [Fact]
    public async Task Enrol_IsRecordedLinkingParticipantEventAndCategory()
    {
        var (_, ev, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        var enrolled = await participant.EnrolSuccessfullyAsync(category.CategoryId);

        var response = await participant.Client.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = await response.ReadAsync<List<EnrolmentDto>>();
        var recorded = Assert.Single(mine);
        Assert.Equal(enrolled.EnrolmentId, recorded.EnrolmentId);
        Assert.Equal(participant.Profile.UserId, recorded.ParticipantId);
        Assert.Equal(participant.Profile.FullName, recorded.ParticipantName);
        Assert.Equal(ev.EventId, recorded.EventId);
        Assert.Equal(ev.Name, recorded.EventName);
        Assert.Equal(category.CategoryId, recorded.CategoryId);
        Assert.Equal(category.Name, recorded.CategoryName);
    }

    [Fact]
    public async Task Enrol_DifferentParticipants_GetDifferentBibNumbers()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var first = await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);
        var second = await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);

        Assert.NotEqual(first.BibNumber, second.BibNumber);
        Assert.Equal(first.BibNumber + 1, second.BibNumber);
    }

    [Fact]
    public async Task Enrol_TwiceInSameCategory_Returns409()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        await participant.EnrolSuccessfullyAsync(category.CategoryId);

        var again = await participant.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_InTwoCategoriesOfTheSameEvent_Returns409()
    {
        var (organiser, ev, first) = await factory.SetupEventAsync();
        var second = await organiser.Client.CreateCategoryAsync(ev.EventId, "5km");
        var participant = await factory.NewUserAsync("Participant");
        await participant.EnrolSuccessfullyAsync(first.CategoryId);

        var response = await participant.Client.EnrolAsync(second.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Anonymous_Returns401()
    {
        var (_, _, category) = await factory.SetupEventAsync();

        var response = await factory.NewClient().EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_Returns403()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();

        var response = await organiser.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.EnrolAsync(999999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_Returns409()
    {
        var (_, _, category) = await factory.SetupEventAsync(maxParticipants: 1);
        await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);
        var late = await factory.NewUserAsync("Participant");

        var response = await late.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InDraftEvent_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var draft = await organiser.Client.CreateEventAsync(status: "Draft");
        var category = await organiser.Client.CreateCategoryAsync(draft.EventId);
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InCancelledEvent_Returns409()
    {
        var (organiser, ev, category) = await factory.SetupEventAsync();
        await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", Api.EventBody("Cancelled one", "Run", "Cancelled"));
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.EnrolAsync(category.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------- Viewing enrolments ----------------

    [Fact]
    public async Task MyEnrolments_OnlyListsTheLoggedInParticipantsOwn()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var alice = await factory.NewUserAsync("Participant");
        var bob = await factory.NewUserAsync("Participant");
        await alice.EnrolSuccessfullyAsync(category.CategoryId);

        var bobsList = await (await bob.Client.GetAsync("/api/users/me/enrolments")).ReadAsync<List<EnrolmentDto>>();

        Assert.Empty(bobsList);
    }

    [Fact]
    public async Task MyEnrolments_Anonymous_Returns401()
    {
        var response = await factory.NewClient().GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MyEnrolments_AsOrganiser_Returns403()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanSeeEnrolmentsOfTheirOwnCategory()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        var enrolled = await participant.EnrolSuccessfullyAsync(category.CategoryId);

        var response = await organiser.Client.GetAsync($"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.ReadAsync<List<EnrolmentDto>>();
        var entry = Assert.Single(list);
        Assert.Equal(enrolled.EnrolmentId, entry.EnrolmentId);
        Assert.Equal(participant.Profile.UserId, entry.ParticipantId);
    }

    [Fact]
    public async Task Organiser_CannotSeeEnrolmentsOfAnotherOrganisersEvent()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var other = await factory.NewUserAsync("Organiser");

        var response = await other.Client.GetAsync($"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewCategoryEnrolments_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.GetAsync($"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CategoryEnrolments_Anonymous_Returns401()
    {
        var (_, _, category) = await factory.SetupEventAsync();

        var response = await factory.NewClient().GetAsync($"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CategoryEnrolments_UnknownCategory_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.GetAsync("/api/categories/999999/enrolments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------- Cancelling ----------------

    [Fact]
    public async Task Cancel_OwnEnrolment_Returns204_AndStatusBecomesCancelled()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        var enrolment = await participant.EnrolSuccessfullyAsync(category.CategoryId);

        var response = await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mine = await (await participant.Client.GetAsync("/api/users/me/enrolments")).ReadAsync<List<EnrolmentDto>>();
        Assert.Equal("Cancelled", Assert.Single(mine).Status);
    }

    [Fact]
    public async Task Cancel_FreesThePlace_AndParticipantCanEnrolAgain()
    {
        var (_, _, category) = await factory.SetupEventAsync(maxParticipants: 1);
        var participant = await factory.NewUserAsync("Participant");
        var first = await participant.EnrolSuccessfullyAsync(category.CategoryId);
        await participant.Client.DeleteAsync($"/api/enrolments/{first.EnrolmentId}");

        var again = await participant.EnrolSuccessfullyAsync(category.CategoryId);

        Assert.Equal("Confirmed", again.Status);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesEnrolment_Returns403()
    {
        var (_, _, category) = await factory.SetupEventAsync();
        var owner = await factory.NewUserAsync("Participant");
        var intruder = await factory.NewUserAsync("Participant");
        var enrolment = await owner.EnrolSuccessfullyAsync(category.CategoryId);

        var response = await intruder.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stillThere = await (await owner.Client.GetAsync("/api/users/me/enrolments")).ReadAsync<List<EnrolmentDto>>();
        Assert.Equal("Confirmed", Assert.Single(stillThere).Status);
    }

    [Fact]
    public async Task Cancel_AsOrganiser_Returns403()
    {
        var (organiser, _, category) = await factory.SetupEventAsync();
        var enrolment = await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_Anonymous_Returns401()
    {
        var response = await factory.NewClient().DeleteAsync("/api/enrolments/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_UnknownEnrolment_Returns404()
    {
        var participant = await factory.NewUserAsync("Participant");

        var response = await participant.Client.DeleteAsync("/api/enrolments/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
