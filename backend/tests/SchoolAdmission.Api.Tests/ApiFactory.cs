using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

// Concurrency tests open up to ~200 connections each; running classes in parallel would only
// measure connection-pool exhaustion, not locking.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Hosts the real API against a real PostgreSQL database (never InMemory/SQLite: they do not
/// implement SELECT … FOR UPDATE, so the concurrency tests would pass for the wrong reason).
/// Each factory has its own DI container, connection pool and fake clock.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const int HoldSeconds = 120;

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? "Host=localhost;Port=5440;Database=school_admission_test;Username=postgres;Password=postgres;Maximum Pool Size=200";

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", HoldSeconds.ToString());
        builder.UseSetting("SWEEPER_INTERVAL_SECONDS", "3600");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }
}

/// <summary>Small typed helpers over the HTTP API so tests read like the contract.</summary>
public static class Api
{
    public static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 100_000_000_000_000_000).ToString();

    public static async Task<string> CreateClassAsync(HttpClient client, int seats, string name = "Test class")
    {
        var response = await client.PostAsJsonAsync("/classes", new { name, totalSeats = seats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    public static async Task<JsonElement> GetClassAsync(HttpClient client, string classId)
    {
        var response = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    public static Task<HttpResponseMessage> HoldAsync(HttpClient client, string classId, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds"), studentId));

    public static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string holdId, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm"), studentId));

    public static Task<HttpResponseMessage> CancelAsync(HttpClient client, string holdId, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}"), studentId));

    public static Task<HttpResponseMessage> MyHoldsAsync(HttpClient client, string? studentId) =>
        client.SendAsync(WithStudent(new HttpRequestMessage(HttpMethod.Get, "/me/holds"), studentId));

    public static async Task<string> HoldIdAsync(HttpClient client, string classId, string studentId)
    {
        var response = await HoldAsync(client, classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("holdId").GetString()!;
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Asserts the status and the standard error body, returning the body.</summary>
    public static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
        return body;
    }

    public static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    private static HttpRequestMessage WithStudent(HttpRequestMessage request, string? studentId)
    {
        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return request;
    }
}
