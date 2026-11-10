using System.Net;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Bonus X1: two independent API hosts (separate DI containers, separate connection pools,
/// nothing shared in memory) pointed at one database must still never exceed totalSeats.
/// Only a lock held inside PostgreSQL can make this pass; an in-process lock would not.
/// </summary>
public sealed class MultiInstanceTests : IAsyncLifetime
{
    private readonly ApiFactory _first = new();
    private readonly ApiFactory _second = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _first.DisposeAsync();
        await _second.DisposeAsync();
    }

    [Fact]
    public async Task MultiInstance_TwoHosts_SharedDb_NeverOverAllocate()
    {
        const int seats = 15;
        const int requests = 200;
        var clientA = _first.CreateClient();
        var clientB = _second.CreateClient();
        var classId = await Api.CreateClassAsync(clientA, seats);

        var start = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, requests).Select(async i =>
        {
            var client = i % 2 == 0 ? clientA : clientB;
            var student = Api.NewStudentId();
            await start.Task;
            return (await Api.HoldAsync(client, classId, student)).StatusCode;
        }).ToList();
        start.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal(seats, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(requests - seats, statuses.Count(s => s == HttpStatusCode.Conflict));

        foreach (var client in new[] { clientA, clientB })
        {
            var counts = await Api.GetClassAsync(client, classId);
            Assert.Equal(seats, counts.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, counts.GetProperty("availableSeats").GetInt32());
        }
    }
}
