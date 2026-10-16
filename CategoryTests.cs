using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class CategoryTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public CategoryTests(RaceDayApiFactory factory) => _factory = factory;

    private static CategoryRequest ValidRequest(string name = "21km") =>
        new() { CategoryName = name, DistanceKM = 21.1m, Fee = 250m };

    [Fact]
    public async Task Organiser_CanAddCategoryToOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<CategoryResponse>();
        Assert.Equal("21km", created!.CategoryName);
        Assert.Equal(ev.EventId, created.EventId);
    }

    [Fact]
    public async Task Participant_CannotAddCategory_ReturnsForbidden()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var other = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await owner.CreateEventAsync();

        var response = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_UnknownEvent_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);

        var response = await organiser.PostAsJsonAsync("/api/events/999999/categories", ValidRequest(), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_NegativeFee_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        var request = ValidRequest();
        request.Fee = -5;

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_DuplicateName_ReturnsConflict()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        await organiser.CreateCategoryAsync(ev.EventId, "Senior");

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", ValidRequest("senior"), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Categories_CanBeViewedWithoutLogin()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventId, "Under 20");
        var anonymous = _factory.NewClient();

        var response = await anonymous.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(await response.ReadAsync<List<CategoryResponse>>() ?? new(), c => c.CategoryId == category.CategoryId);
    }

    [Fact]
    public async Task Participant_CanViewCategories()
    {
        var organiser = await _factory.LoggedInClientAsync(TestHelpers.OrganiserRoleId);
        var participant = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);
        var ev = await organiser.CreateEventAsync();
        await organiser.CreateCategoryAsync(ev.EventId);

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_UnknownEvent_ReturnsNotFound()
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
