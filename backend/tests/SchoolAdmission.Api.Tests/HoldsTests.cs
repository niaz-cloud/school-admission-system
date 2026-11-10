using System.Net;

namespace SchoolAdmission.Api.Tests;

public sealed class HoldsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Hold_HappyPath_HeldThenConfirmed()
    {
        var classId = await Api.CreateClassAsync(_client, 3, "Class 6 - Morning Shift");
        var student = Api.NewStudentId();

        var created = await Api.HoldAsync(_client, classId, student);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var hold = await Api.ReadJsonAsync(created);
        var holdId = hold.GetProperty("holdId").GetString()!;
        Assert.StartsWith("h_", holdId);
        Assert.Equal(classId, hold.GetProperty("classId").GetString());
        Assert.Equal(student, hold.GetProperty("studentId").GetString());
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", hold.GetProperty("expiresAt").GetString());

        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(1, counts.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, counts.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(2, counts.GetProperty("availableSeats").GetInt32());

        var fetched = await Api.ReadJsonAsync(await _client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("HELD", fetched.GetProperty("status").GetString());
        Assert.Equal(classId, fetched.GetProperty("classId").GetString());

        var confirmed = await Api.ConfirmAsync(_client, holdId, student);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmedBody = await Api.ReadJsonAsync(confirmed);
        Assert.Equal(holdId, confirmedBody.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", confirmedBody.GetProperty("status").GetString());

        counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(0, counts.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, counts.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(2, counts.GetProperty("availableSeats").GetInt32());

        var mine = await Api.ReadJsonAsync(await Api.MyHoldsAsync(_client, student));
        var entry = Assert.Single(mine.EnumerateArray());
        Assert.Equal(holdId, entry.GetProperty("holdId").GetString());
        Assert.Equal("Class 6 - Morning Shift", entry.GetProperty("className").GetString());
        Assert.Equal("CONFIRMED", entry.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirm_Twice_CountsOnce()
    {
        var classId = await Api.CreateClassAsync(_client, 5);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);

        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(_client, holdId, student)).StatusCode);
        var second = await Api.ConfirmAsync(_client, holdId, student);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("CONFIRMED", (await Api.ReadJsonAsync(second)).GetProperty("status").GetString());

        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(1, counts.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, counts.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Confirm_OtherStudentsHold_Returns403()
    {
        var classId = await Api.CreateClassAsync(_client, 5);
        var owner = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, owner);

        await Api.AssertProblemAsync(await Api.ConfirmAsync(_client, holdId, Api.NewStudentId()), HttpStatusCode.Forbidden, "NOT_HOLD_OWNER");
        await Api.AssertProblemAsync(await Api.CancelAsync(_client, holdId, Api.NewStudentId()), HttpStatusCode.Forbidden, "NOT_HOLD_OWNER");

        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(0, counts.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1, counts.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Hold_Expires_SeatReleased_And_ConfirmFails()
    {
        var classId = await Api.CreateClassAsync(_client, 1);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);
        Assert.Equal(0, (await Api.GetClassAsync(_client, classId)).GetProperty("availableSeats").GetInt32());
        await Api.AssertProblemAsync(await Api.HoldAsync(_client, classId, Api.NewStudentId()), HttpStatusCode.Conflict, "SEATS_FULL");

        // No API call releases the seat: time passing is enough.
        factory.Time.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds + 1));

        var hold = await Api.ReadJsonAsync(await _client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());
        var counts = await Api.GetClassAsync(_client, classId);
        Assert.Equal(0, counts.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, counts.GetProperty("availableSeats").GetInt32());

        await Api.AssertProblemAsync(await Api.ConfirmAsync(_client, holdId, student), HttpStatusCode.Conflict, "HOLD_EXPIRED");
        await Api.AssertProblemAsync(await Api.CancelAsync(_client, holdId, student), HttpStatusCode.Conflict, "HOLD_EXPIRED");

        // The freed seat can be taken by someone else.
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, classId, Api.NewStudentId())).StatusCode);
    }

    [Fact]
    public async Task Hold_StillValidJustBeforeExpiry()
    {
        var classId = await Api.CreateClassAsync(_client, 1);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);

        factory.Time.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds - 1));
        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(_client, holdId, student)).StatusCode);
    }

    [Fact]
    public async Task SameStudent_SecondHoldSameClass_Returns409()
    {
        var classId = await Api.CreateClassAsync(_client, 5);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);

        var body = await Api.AssertProblemAsync(await Api.HoldAsync(_client, classId, student), HttpStatusCode.Conflict, "ALREADY_APPLIED");
        Assert.Equal(holdId, body.GetProperty("holdId").GetString());

        // Allowed again after cancelling.
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CancelAsync(_client, holdId, student)).StatusCode);
        var second = await Api.HoldIdAsync(_client, classId, student);

        // Allowed again after expiry.
        factory.Time.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds + 1));
        var third = await Api.HoldIdAsync(_client, classId, student);
        Assert.NotEqual(second, third);

        // Not allowed while confirmed.
        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(_client, third, student)).StatusCode);
        await Api.AssertProblemAsync(await Api.HoldAsync(_client, classId, student), HttpStatusCode.Conflict, "ALREADY_APPLIED");

        // A different class is fine.
        var otherClass = await Api.CreateClassAsync(_client, 5);
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(_client, otherClass, student)).StatusCode);
    }

    [Fact]
    public async Task Cancel_ReleasesSeat_AndIsIdempotent()
    {
        var classId = await Api.CreateClassAsync(_client, 1);
        var student = Api.NewStudentId();
        var holdId = await Api.HoldIdAsync(_client, classId, student);

        var first = await Api.CancelAsync(_client, holdId, student);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Empty(await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CancelAsync(_client, holdId, student)).StatusCode);

        var hold = await Api.ReadJsonAsync(await _client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("CANCELLED", hold.GetProperty("status").GetString());
        Assert.Equal(1, (await Api.GetClassAsync(_client, classId)).GetProperty("availableSeats").GetInt32());
        await Api.AssertProblemAsync(await Api.ConfirmAsync(_client, holdId, student), HttpStatusCode.Conflict, "HOLD_CANCELLED");

        var confirmedId = await Api.HoldIdAsync(_client, classId, student);
        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(_client, confirmedId, student)).StatusCode);
        await Api.AssertProblemAsync(await Api.CancelAsync(_client, confirmedId, student), HttpStatusCode.Conflict, "HOLD_ALREADY_CONFIRMED");
    }

    [Fact]
    public async Task MissingOrBadStudentId_Returns400()
    {
        var classId = await Api.CreateClassAsync(_client, 5);
        var holdId = await Api.HoldIdAsync(_client, classId, Api.NewStudentId());

        await Api.AssertProblemAsync(await Api.HoldAsync(_client, classId, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
        await Api.AssertProblemAsync(await Api.MyHoldsAsync(_client, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
        await Api.AssertProblemAsync(await Api.ConfirmAsync(_client, holdId, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
        await Api.AssertProblemAsync(await Api.CancelAsync(_client, holdId, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");

        foreach (var bad in new[] { "1234567890123456", "123456789012345678", "1234567890123456a", "২০১৫২৬৯১২৩৪৫৬৭৮৯০" })
        {
            await Api.AssertProblemAsync(await Api.HoldAsync(_client, classId, bad), HttpStatusCode.BadRequest, "STUDENT_ID_INVALID");
        }
    }

    [Fact]
    public async Task UnknownIds_Return404()
    {
        var student = Api.NewStudentId();
        await Api.AssertProblemAsync(await Api.HoldAsync(_client, "c_missing", student), HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
        await Api.AssertProblemAsync(await _client.GetAsync("/holds/h_missing"), HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
        await Api.AssertProblemAsync(await Api.ConfirmAsync(_client, "h_missing", student), HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
        await Api.AssertProblemAsync(await Api.CancelAsync(_client, "h_missing", student), HttpStatusCode.NotFound, "HOLD_NOT_FOUND");
    }

    [Fact]
    public async Task MyHolds_NewestFirst_WithEffectiveStatus()
    {
        var student = Api.NewStudentId();
        var classA = await Api.CreateClassAsync(_client, 5, "A");
        var first = await Api.HoldIdAsync(_client, classA, student);
        factory.Time.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds + 1));
        var classB = await Api.CreateClassAsync(_client, 5, "B");
        var second = await Api.HoldIdAsync(_client, classB, student);

        var mine = (await Api.ReadJsonAsync(await Api.MyHoldsAsync(_client, student))).EnumerateArray().ToList();
        Assert.Equal(2, mine.Count);
        Assert.Equal(second, mine[0].GetProperty("holdId").GetString());
        Assert.Equal("HELD", mine[0].GetProperty("status").GetString());
        Assert.Equal(first, mine[1].GetProperty("holdId").GetString());
        Assert.Equal("EXPIRED", mine[1].GetProperty("status").GetString());

        Assert.Empty((await Api.ReadJsonAsync(await Api.MyHoldsAsync(_client, Api.NewStudentId()))).EnumerateArray());
    }
}
