using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class ResultTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ResultTests(RaceDayApiFactory factory) => _factory = factory;

    /// <summary>An organiser, a participant enrolled in the organiser's event, and the event date moved into the past.</summary>
    private async Task<(HttpClient Organiser, HttpClient Participant, EventResponse Event, EnrolmentResponse Enrolment)> ArrangeFinishedEventAsync()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);
        await organiser.SetEventDateAsync(ev, DateTime.UtcNow.AddDays(-1));
        return (organiser, participant, ev, enrolment);
    }

    private static ResultRequest Request(int enrolmentId, int position = 1) =>
        new() { EnrolmentId = enrolmentId, FinishTimeSeconds = 3725, Position = position };

    [Fact]
    public async Task Organiser_CanRecordResult_AfterTheEvent()
    {
        var (organiser, _, _, enrolment) = await ArrangeFinishedEventAsync();

        var response = await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.ReadAsync<ResultResponse>();
        Assert.Equal(3725, result!.FinishTimeSeconds);
        Assert.Equal("01:02:05", result.FinishTime);
        Assert.Equal(1, result.Position);
    }

    [Fact]
    public async Task Participant_CanViewOwnResults()
    {
        var (organiser, participant, ev, enrolment) = await ArrangeFinishedEventAsync();
        await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId, 7), TestHelpers.Json);

        var response = await participant.GetAsync("/api/results/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.ReadAsync<List<ResultResponse>>();
        var mine = Assert.Single(results!);
        Assert.Equal(ev.EventId, mine.EventId);
        Assert.Equal(7, mine.Position);
        Assert.Equal(1, mine.TotalFinishers);
    }

    [Fact]
    public async Task EventResults_AreVisibleWithoutLogin()
    {
        var (organiser, _, ev, enrolment) = await ArrangeFinishedEventAsync();
        await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);
        var anonymous = _factory.NewClient();

        var response = await anonymous.GetAsync($"/api/events/{ev.EventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.ReadAsync<List<ResultResponse>>())!);
    }

    [Fact]
    public async Task EventResults_UnknownEvent_ReturnsNotFound()
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync("/api/events/999999/results");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResult_ReturnsForbidden()
    {
        var (_, participant, _, enrolment) = await ArrangeFinishedEventAsync();

        var response = await participant.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotRecordResult_ReturnsUnauthorized()
    {
        var anonymous = _factory.NewClient();

        var response = await anonymous.PostAsJsonAsync("/api/results", Request(1), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_AsOrganiser_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.GetAsync("/api/results/my");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_BeforeTheEvent_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);

        var response = await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_UnknownEnrolment_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.PostAsJsonAsync("/api/results", Request(999999), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForAnotherOrganisersEvent_ReturnsForbidden()
    {
        var (_, _, _, enrolment) = await ArrangeFinishedEventAsync();
        var otherOrganiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await otherOrganiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_Twice_ReturnsConflict()
    {
        var (organiser, _, _, enrolment) = await ArrangeFinishedEventAsync();
        await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        var response = await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId, 2), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_DuplicatePositionInSameEvent_ReturnsConflict()
    {
        var (organiser, _, ev, firstEnrolment) = await ArrangeFinishedEventAsync();
        var secondParticipant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        // The event is already in the past, so enrol the second participant by temporarily moving it into the future.
        await organiser.SetEventDateAsync(ev, DateTime.UtcNow.AddDays(5));
        var category = (await (await organiser.GetAsync($"/api/events/{ev.EventId}/categories")).ReadAsync<List<CategoryResponse>>())![0];
        var secondEnrolment = await secondParticipant.EnrolAsync(category.CategoryId);
        await organiser.SetEventDateAsync(ev, DateTime.UtcNow.AddDays(-1));

        await organiser.PostAsJsonAsync("/api/results", Request(firstEnrolment.EnrolmentId, 1), TestHelpers.Json);
        var response = await organiser.PostAsJsonAsync("/api/results", Request(secondEnrolment.EnrolmentId, 1), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3600, 0)]
    public async Task RecordResult_InvalidValues_ReturnsBadRequest(int seconds, int position)
    {
        var (organiser, _, _, enrolment) = await ArrangeFinishedEventAsync();

        var response = await organiser.PostAsJsonAsync("/api/results",
            new ResultRequest { EnrolmentId = enrolment.EnrolmentId, FinishTimeSeconds = seconds, Position = position },
            TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordResult_ForCancelledEnrolment_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);
        await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        await organiser.SetEventDateAsync(ev, DateTime.UtcNow.AddDays(-1));

        var response = await organiser.PostAsJsonAsync("/api/results", Request(enrolment.EnrolmentId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
