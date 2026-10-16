using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests;

public static class TestHelpers
{
    public const string OrganiserCode = "RACEDAY-ORG-2026";
    public const string Password = "Passw0rd123";
    public const int OrganiserRoleId = RoleNames.OrganiserRoleId;
    public const int ParticipantRoleId = RoleNames.ParticipantRoleId;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>A client with its own cookie jar, so each client behaves like a separate browser session.</summary>
    public static HttpClient NewClient(this RaceDayApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    public static string UniqueEmail() => $"user{Guid.NewGuid():N}@raceday.test";

    public static RegisterRequest NewRegisterRequest(int roleId, string? email = null, string? code = null) => new()
    {
        FullName = "Test User",
        Email = email ?? UniqueEmail(),
        Password = Password,
        RoleId = roleId,
        OrganiserCode = code
    };

    /// <summary>Registers a new user and logs them in. The returned client holds the session cookie.</summary>
    public static async Task<HttpClient> LoggedInClientAsync(this RaceDayApiFactory factory, int roleId)
    {
        var client = factory.NewClient();
        var email = UniqueEmail();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            NewRegisterRequest(roleId, email, roleId == OrganiserRoleId ? OrganiserCode : null), Json);
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password }, Json);
        login.EnsureSuccessStatusCode();

        return client;
    }

    public static Task<T?> ReadAsync<T>(this HttpResponseMessage response) => response.Content.ReadFromJsonAsync<T>(Json);

    public static async Task<EventResponse> CreateEventAsync(
        this HttpClient organiser, DateTime? date = null, EventType type = EventType.Run, string? title = null)
    {
        var request = new EventRequest
        {
            Title = title ?? $"Test Race {Guid.NewGuid():N}",
            Description = "A test event",
            EventDate = date ?? DateTime.UtcNow.AddDays(30),
            Location = "Johannesburg",
            DistanceKM = 10,
            EventType = type
        };

        var response = await organiser.PostAsJsonAsync("/api/events", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<EventResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(
        this HttpClient organiser, int eventId, string name = "10km", decimal distance = 10m, decimal fee = 100m)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new CategoryRequest { CategoryName = name, DistanceKM = distance, Fee = fee }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<CategoryResponse>())!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(this HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsJsonAsync("/api/enrolments", new EnrolRequest { CategoryId = categoryId }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<EnrolmentResponse>())!;
    }

    public static async Task SetEventDateAsync(this HttpClient organiser, EventResponse ev, DateTime date)
    {
        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", new EventRequest
        {
            Title = ev.Title,
            Description = ev.Description,
            EventDate = date,
            Location = ev.Location,
            DistanceKM = ev.DistanceKM,
            EventType = ev.EventType
        }, Json);
        response.EnsureSuccessStatusCode();
    }
}
