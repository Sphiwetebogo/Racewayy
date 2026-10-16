using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public class EventTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventTests(RaceDayApiFactory factory) => _factory = factory;

    private static EventRequest ValidRequest() => new()
    {
        Title = "City Marathon",
        Description = "A road race",
        EventDate = DateTime.UtcNow.AddDays(20),
        Location = "Durban",
        DistanceKM = 42.2m,
        EventType = EventType.Run
    };

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.PostAsJsonAsync("/api/events", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<EventResponse>();
        Assert.Equal("City Marathon", created!.Title);
        Assert.Equal(EventType.Run, created.EventType);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_ReturnsForbidden()
    {
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await participant.PostAsJsonAsync("/api/events", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_ReturnsUnauthorized()
    {
        var anonymous = _factory.NewClient();

        var response = await anonymous.PostAsJsonAsync("/api/events", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingTitle_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var request = ValidRequest();
        request.Title = "";

        var response = await organiser.PostAsJsonAsync("/api/events", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ZeroDistance_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var request = ValidRequest();
        request.DistanceKM = 0;

        var response = await organiser.PostAsJsonAsync("/api/events", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingEventType_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var request = ValidRequest();
        request.EventType = null;

        var response = await organiser.PostAsJsonAsync("/api/events", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Events_CanBeViewedWithoutLogin()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        var anonymous = _factory.NewClient();

        var list = await anonymous.GetAsync("/api/events");
        var single = await anonymous.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(await list.ReadAsync<List<EventResponse>>() ?? new(), e => e.EventId == ev.EventId);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEvents()
    {
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await participant.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_Unknown_ReturnsNotFound()
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_FilterByType_ReturnsOnlyMatchingEvents()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var cycle = await organiser.CreateEventAsync(type: EventType.Cycle);
        await organiser.CreateEventAsync(type: EventType.Walk);

        var response = await organiser.GetAsync("/api/events?eventType=Cycle");
        var events = await response.ReadAsync<List<EventResponse>>();

        Assert.NotEmpty(events!);
        Assert.All(events!, e => Assert.Equal(EventType.Cycle, e.EventType));
        Assert.Contains(events!, e => e.EventId == cycle.EventId);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        var request = ValidRequest();
        request.Title = "Renamed Race";

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed Race", (await response.ReadAsync<EventResponse>())!.Title);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var other = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await owner.CreateEventAsync();

        var response = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEvent_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_Unknown_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.PutAsJsonAsync("/api/events/999999", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        var afterDelete = await organiser.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_RemovesItsCategoriesAndEnrolments()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        var myEnrolments = await (await participant.GetAsync("/api/enrolments/my")).ReadAsync<List<EnrolmentResponse>>();

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(myEnrolments!, e => e.EventId == ev.EventId);
    }

    [Fact]
    public async Task Participant_CannotDeleteEvent_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var other = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await owner.CreateEventAsync();

        var response = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
