using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Results: Organisers capture them, Participants view their own, event results are public.</summary>
public class ResultTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private static object Finished(string time = "00:45:30", int position = 1) =>
        new { finishTime = time, position, status = "Finished" };

    private async Task<(TestUser Organiser, EventDto Event, CategoryDto Category, TestUser Participant, EnrolmentDto Enrolment)> ScenarioAsync()
    {
        var (organiser, ev, category) = await factory.SetupEventAsync();
        var participant = await factory.NewUserAsync("Participant");
        var enrolment = await participant.EnrolSuccessfullyAsync(category.CategoryId);
        return (organiser, ev, category, participant, enrolment);
    }

    // ---------------- Record ----------------

    [Fact]
    public async Task RecordResult_AsOwningOrganiser_Returns201()
    {
        var (organiser, ev, category, participant, enrolment) = await ScenarioAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished("00:24:37", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadAsync<ResultDto>();
        Assert.Equal(enrolment.EnrolmentId, result.EnrolmentId);
        Assert.Equal(new TimeOnly(0, 24, 37), result.FinishTime);
        Assert.Equal(1, result.Position);
        Assert.Equal("Finished", result.Status);
        Assert.Equal(ev.EventId, result.EventId);
        Assert.Equal(category.Name, result.CategoryName);
        Assert.Equal(participant.Profile.FullName, result.ParticipantName);
    }

    [Fact]
    public async Task RecordResult_AsParticipant_Returns403()
    {
        var (_, _, _, participant, enrolment) = await ScenarioAsync();

        var response = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_Anonymous_Returns401()
    {
        var (_, _, _, _, enrolment) = await ScenarioAsync();

        var response = await factory.NewClient().PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ByAnotherOrganiser_Returns403()
    {
        var (_, _, _, _, enrolment) = await ScenarioAsync();
        var other = await factory.NewUserAsync("Organiser");

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_UnknownEnrolment_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PostAsJsonAsync("/api/enrolments/999999/results", Finished());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_Twice_Returns409()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        var again = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished("00:50:00", 2));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task RecordResult_FinishedWithoutTime_Returns400()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { position = 1, status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_FinishedWithoutPosition_Returns400()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { finishTime = "00:30:00", status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_InvalidStatus_Returns400()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results",
            new { finishTime = "00:30:00", position = 1, status = "Winner" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_DidNotFinish_NeedsNoTimeOrPosition()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", new { status = "DNF" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadAsync<ResultDto>();
        Assert.Equal("DNF", result.Status);
        Assert.Null(result.FinishTime);
        Assert.Null(result.Position);
    }

    [Fact]
    public async Task RecordResult_PositionAlreadyTakenInCategory_Returns409()
    {
        var (organiser, _, category, _, first) = await ScenarioAsync();
        var second = await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{first.EnrolmentId}/results", Finished("00:30:00", 1));

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{second.EnrolmentId}/results", Finished("00:31:00", 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForCancelledEnrolment_Returns409()
    {
        var (organiser, _, _, participant, enrolment) = await ScenarioAsync();
        await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CancelEnrolment_AfterResultRecorded_Returns409()
    {
        var (organiser, _, _, participant, enrolment) = await ScenarioAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished());

        var response = await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateResult_ByOwningOrganiser_Returns200()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();
        var created = await (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished("00:30:00", 2))).ReadAsync<ResultDto>();

        var response = await organiser.Client.PutAsJsonAsync($"/api/results/{created.ResultId}", Finished("00:29:15", 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<ResultDto>();
        Assert.Equal(new TimeOnly(0, 29, 15), updated.FinishTime);
        Assert.Equal(1, updated.Position);
    }

    [Fact]
    public async Task UpdateResult_KeepingOwnPosition_IsNotAConflict()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();
        var created = await (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished("00:30:00", 3))).ReadAsync<ResultDto>();

        var response = await organiser.Client.PutAsJsonAsync($"/api/results/{created.ResultId}", Finished("00:30:05", 3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_ByParticipant_Returns403()
    {
        var (organiser, _, _, participant, enrolment) = await ScenarioAsync();
        var created = await (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished())).ReadAsync<ResultDto>();

        var response = await participant.Client.PutAsJsonAsync($"/api/results/{created.ResultId}", Finished("00:01:00", 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_ByAnotherOrganiser_Returns403()
    {
        var (organiser, _, _, _, enrolment) = await ScenarioAsync();
        var created = await (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished())).ReadAsync<ResultDto>();
        var other = await factory.NewUserAsync("Organiser");

        var response = await other.Client.PutAsJsonAsync($"/api/results/{created.ResultId}", Finished("00:01:00", 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_Unknown_Returns404()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.PutAsJsonAsync("/api/results/999999", Finished());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------- Viewing ----------------

    [Fact]
    public async Task EventResults_ArePublic_AndListRecordedResults()
    {
        var (organiser, ev, _, _, enrolment) = await ScenarioAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/results", Finished("00:40:00", 1));

        var response = await factory.NewClient().GetAsync($"/api/events/{ev.EventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.ReadAsync<List<ResultDto>>();
        var entry = Assert.Single(results);
        Assert.Equal(enrolment.EnrolmentId, entry.EnrolmentId);
    }

    [Fact]
    public async Task EventResults_AreOrderedByPosition()
    {
        var (organiser, ev, category, _, first) = await ScenarioAsync();
        var second = await (await factory.NewUserAsync("Participant")).EnrolSuccessfullyAsync(category.CategoryId);
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{first.EnrolmentId}/results", Finished("00:50:00", 2));
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{second.EnrolmentId}/results", Finished("00:40:00", 1));

        var results = await (await factory.NewClient().GetAsync($"/api/events/{ev.EventId}/results")).ReadAsync<List<ResultDto>>();

        Assert.Equal(new int?[] { 1, 2 }, results.Select(r => r.Position).ToArray());
    }

    [Fact]
    public async Task EventResults_EventWithNoResultsYet_ReturnsEmptyList()
    {
        var (_, ev, _) = await factory.SetupEventAsync();

        var response = await factory.NewClient().GetAsync($"/api/events/{ev.EventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsync<List<ResultDto>>());
    }

    [Fact]
    public async Task EventResults_UnknownEvent_Returns404()
    {
        var response = await factory.NewClient().GetAsync("/api/events/999999/results");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_ParticipantSeesOnlyTheirOwn()
    {
        var (organiser, _, category, participant, mine) = await ScenarioAsync();
        var otherParticipant = await factory.NewUserAsync("Participant");
        var theirs = await otherParticipant.EnrolSuccessfullyAsync(category.CategoryId);
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{mine.EnrolmentId}/results", Finished("00:40:00", 1));
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{theirs.EnrolmentId}/results", Finished("00:41:00", 2));

        var response = await participant.Client.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.ReadAsync<List<ResultDto>>();
        var entry = Assert.Single(results);
        Assert.Equal(mine.EnrolmentId, entry.EnrolmentId);
        Assert.Equal(1, entry.Position);
    }

    [Fact]
    public async Task MyResults_AsOrganiser_Returns403()
    {
        var organiser = await factory.NewUserAsync("Organiser");

        var response = await organiser.Client.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_Anonymous_Returns401()
    {
        var response = await factory.NewClient().GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
