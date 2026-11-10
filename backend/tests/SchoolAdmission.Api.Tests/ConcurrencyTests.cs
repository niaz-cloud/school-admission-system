using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Tests;

/// <summary>Rule B1/B3/B5 under real contention: many requests released at once against one class.</summary>
public sealed class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Concurrency_ManyStudents_NeverOverAllocate()
    {
        const int seats = 10;
        const int requests = 200;
        var classId = await Api.CreateClassAsync(_client, seats);

        var start = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, requests).Select(async _ =>
        {
            var student = Api.NewStudentId();
            await start.Task;
            var response = await Api.HoldAsync(_client, classId, student);
            var code = response.StatusCode == HttpStatusCode.Created
                ? null
                : (await Api.ReadJsonAsync(response)).GetProperty("code").GetString();
            return (response.StatusCode, code);
        }).ToList();
        start.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(seats, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(requests - seats, results.Count(r => r.StatusCode == HttpStatusCode.Conflict && r.code == "SEATS_FULL"));

        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(seats, counts.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, counts.GetProperty("availableSeats").GetInt32());
        Assert.Equal(seats, await CountActiveHoldsInDatabaseAsync(classId));
    }

    [Fact]
    public async Task Concurrency_SameStudent_OnlyOneHold()
    {
        var classId = await Api.CreateClassAsync(_client, 100);
        var student = Api.NewStudentId();

        var start = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            await start.Task;
            return await Api.HoldAsync(_client, classId, student);
        }).ToList();
        start.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await Api.AssertProblemAsync(rejected, HttpStatusCode.Conflict, "ALREADY_APPLIED");
        }

        Assert.Equal(1, await CountActiveHoldsInDatabaseAsync(classId));
    }

    [Fact]
    public async Task Concurrency_ConfirmRace_CountsOnce()
    {
        var classId = await Api.CreateClassAsync(_client, 5);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);

        var start = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await start.Task;
            return await Api.ConfirmAsync(_client, holdId, student);
        }).ToList();
        start.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(1, counts.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, counts.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Concurrency_MixedHoldsCancelsAndConfirms_NeverOverAllocate()
    {
        const int seats = 5;
        var classId = await Api.CreateClassAsync(_client, seats);

        // Several waves of contention where winners randomly confirm or cancel, freeing seats
        // for the next wave. The invariant must hold after every wave.
        for (var wave = 0; wave < 5; wave++)
        {
            var start = new TaskCompletionSource();
            var tasks = Enumerable.Range(0, 60).Select(async _ =>
            {
                var student = Api.NewStudentId();
                await start.Task;
                var response = await Api.HoldAsync(_client, classId, student);
                if (response.StatusCode != HttpStatusCode.Created)
                {
                    return;
                }

                var holdId = (await Api.ReadJsonAsync(response)).GetProperty("holdId").GetString()!;
                if (Random.Shared.Next(2) == 0)
                {
                    await Api.CancelAsync(_client, holdId, student);
                }
                else
                {
                    await Api.ConfirmAsync(_client, holdId, student);
                }
            }).ToList();
            start.SetResult();
            await Task.WhenAll(tasks);

            var counts = await Api.GetClassAsync(_client, classId);
            var used = counts.GetProperty("heldSeats").GetInt32() + counts.GetProperty("confirmedSeats").GetInt32();
            Assert.True(used <= seats, $"wave {wave}: {used} seats used of {seats}");
            Assert.True(await CountActiveHoldsInDatabaseAsync(classId) <= seats);
        }
    }

    private async Task<int> CountActiveHoldsInDatabaseAsync(string classId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Holds.CountAsync(h => h.ClassId == classId && (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED));
    }
}
