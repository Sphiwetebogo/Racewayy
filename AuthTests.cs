using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class AuthTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Participant_ReturnsCreated()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.ReadAsync<UserResponse>();
        Assert.Equal("Participant", user!.Role);
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_Organiser_WithCorrectCode_ReturnsCreated()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.OrganiserRoleId, code: TestHelpers.OrganiserCode), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Organiser", (await response.ReadAsync<UserResponse>())!.Role);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.OrganiserRoleId), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.OrganiserRoleId, code: "NOT-THE-CODE"), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var client = _factory.NewClient();
        var email = TestHelpers.UniqueEmail();

        var first = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, email), TestHelpers.Json);
        var second = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, email.ToUpperInvariant()), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, "not-an-email"), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletterspassword")]
    [InlineData("1234567890")]
    public async Task Register_WeakPassword_ReturnsBadRequest(string password)
    {
        var client = _factory.NewClient();
        var request = TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId);
        request.Password = password;

        var response = await client.PostAsJsonAsync("/api/auth/register", request, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRoleId_ReturnsBadRequest()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(roleId: 99), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotThePlainPassword()
    {
        var client = _factory.NewClient();
        var email = TestHelpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, email), TestHelpers.Json);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == email.ToLowerInvariant());

        Assert.NotEqual(TestHelpers.Password, user.PasswordHash);
        Assert.DoesNotContain(TestHelpers.Password, user.PasswordHash);
    }

    [Theory]
    [InlineData(TestHelpers.ParticipantRoleId, "Participant")]
    [InlineData(TestHelpers.OrganiserRoleId, "Organiser")]
    public async Task Login_ValidCredentials_ReturnsOkWithRole(int roleId, string expectedRole)
    {
        var client = _factory.NewClient();
        var email = TestHelpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(roleId, email, roleId == TestHelpers.OrganiserRoleId ? TestHelpers.OrganiserCode : null),
            TestHelpers.Json);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = TestHelpers.Password }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedRole, (await response.ReadAsync<UserResponse>())!.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();
        var email = TestHelpers.UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register",
            TestHelpers.NewRegisterRequest(TestHelpers.ParticipantRoleId, email), TestHelpers.Json);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword1" }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = TestHelpers.UniqueEmail(), Password = TestHelpers.Password }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_ReturnsUnauthorized()
    {
        var client = _factory.NewClient();

        var response = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_KeepsUserLoggedInAcrossRequests()
    {
        var client = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var first = await client.GetAsync("/api/users/profile");
        var second = await client.GetAsync("/api/enrolments/my");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await _factory.LoggedInClientAsync(TestHelpers.ParticipantRoleId);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
