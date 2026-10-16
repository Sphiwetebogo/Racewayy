using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EnrolmentTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await participant.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = category.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<EnrolmentResponse>();
        Assert.Equal(PaymentStatus.Pending, created!.PaymentStatus);

        var mine = await (await participant.GetAsync("/api/enrolments/my")).ReadAsync<List<EnrolmentResponse>>();
        var recorded = Assert.Single(mine!, e => e.EnrolmentId == created.EnrolmentId);
        Assert.Equal(ev.EventId, recorded.EventId);
        Assert.Equal(category.CategoryId, recorded.CategoryId);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await organiser.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = category.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_ReturnsUnauthorized()
    {
        var anonymous = _factory.NewClient();

        var response = await anonymous.PostAsJsonAsync("/api/enrolments", new EnrolRequest { CategoryId = 1 }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_ReturnsConflict()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        await participant.EnrolAsync(category.CategoryId);

        var response = await participant.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = category.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_SecondCategoryOfSameEvent_ReturnsConflict()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var first = await organiser.CreateCategoryAsync(ev.EventId, "10km");
        var second = await organiser.CreateCategoryAsync(ev.EventId, "21km", 21.1m);
        await participant.EnrolAsync(first.CategoryId);

        var response = await participant.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = second.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolRequest { CategoryId = 999999 }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InvalidCategoryId_ReturnsBadRequest()
    {
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolRequest { CategoryId = 0 }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_ForEventThatAlreadyHappened_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync(date: DateTime.UtcNow.AddDays(-2));
        var category = await organiser.CreateCategoryAsync(ev.EventId);

        var response = await participant.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = category.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MyEnrolments_AsOrganiser_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.GetAsync("/api/enrolments/my");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelEnrolment_AndEnrolAgain()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var mine = await (await participant.GetAsync("/api/enrolments/my")).ReadAsync<List<EnrolmentResponse>>();
        var again = await participant.PostAsJsonAsync("/api/enrolments",
            new EnrolRequest { CategoryId = category.CategoryId }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(PaymentStatus.Cancelled, mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).PaymentStatus);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelAnotherParticipantsEnrolment_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var owner = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var other = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await owner.EnrolAsync(category.CategoryId);

        var response = await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId);
        var enrolment = await participant.EnrolAsync(category.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.ReadAsync<List<EnrolmentResponse>>();
        Assert.Contains(list!, e => e.EnrolmentId == enrolment.EnrolmentId && e.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var other = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await owner.CreateEventAsync();

        var response = await other.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_UnknownEvent_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.GetAsync("/api/events/999999/enrolments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
