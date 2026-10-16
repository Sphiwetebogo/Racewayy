using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class UserProfileTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public UserProfileTests(RaceDayApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(TestHelpers.ParticipantRoleId)]
    [InlineData(TestHelpers.OrganiserRoleId)]
    public async Task GetProfile_BothRoles_ReturnOwnProfile(int roleId)
    {
        var client = await _factory.LoggedInClientAsync(roleId);

        var response = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(roleId, (await response.ReadAsync<UserResponse>())!.RoleId);
    }

    [Theory]
    [InlineData(TestHelpers.ParticipantRoleId)]
    [InlineData(TestHelpers.OrganiserRoleId)]
    public async Task UpdateProfile_BothRoles_PersistsChanges(int roleId)
    {
        var client = await _factory.LoggedInClientAsync(roleId);
        var newEmail = TestHelpers.UniqueEmail();

        var update = await client.PutAsJsonAsync("/api/users/profile",
            new UpdateProfileRequest { FullName = "Updated Name", Email = newEmail }, TestHelpers.Json);
        var profile = await (await client.GetAsync("/api/users/profile")).ReadAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated Name", profile!.FullName);
        Assert.Equal(newEmail.ToLowerInvariant(), profile.Email);
    }

    [Fact]
    public async Task UpdateProfile_EmailAlreadyUsed_ReturnsBadRequest()
    {
        var other = _factory.NewClient();
        var takenEmail = TestHelpers.UniqueEmail();
        await other.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, takenEmail), TestHelpers.Json);
        var client = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await client.PutAsJsonAsync("/api/users/profile",
            new UpdateProfileRequest { FullName = "Someone", Email = takenEmail }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_InvalidEmail_ReturnsBadRequest()
    {
        var client = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var response = await client.PutAsJsonAsync("/api/users/profile",
            new UpdateProfileRequest { FullName = "Someone", Email = "nope" }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutLogin_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();

        var response = await client.PutAsJsonAsync("/api/users/profile",
            new UpdateProfileRequest { FullName = "Someone", Email = TestHelpers.UniqueEmail() }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
