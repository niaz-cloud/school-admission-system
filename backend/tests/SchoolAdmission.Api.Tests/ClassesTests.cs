using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests;

public sealed class ClassesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateClass_ReturnsContractShape()
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name = "  Class 6 - Morning Shift  ", totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Api.ReadJsonAsync(response);
        var id = body.GetProperty("id").GetString()!;
        Assert.StartsWith("c_", id);
        Assert.Equal("Class 6 - Morning Shift", body.GetProperty("name").GetString());
        Assert.Equal(120, body.GetProperty("totalSeats").GetInt32());
        Assert.Equal($"/classes/{id}", response.Headers.Location?.OriginalString);

        var list = await Api.ReadJsonAsync(await _client.GetAsync("/classes"));
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        var listed = list.EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        Assert.Equal(120, listed.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, listed.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, listed.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(120, listed.GetProperty("availableSeats").GetInt32());

        var single = await Api.GetClassAsync(_client, id);
        Assert.Equal("Class 6 - Morning Shift", single.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateClass_DuplicateNames_AreAllowed()
    {
        var first = await Api.CreateClassAsync(_client, 5, "Duplicate name");
        var second = await Api.CreateClassAsync(_client, 5, "Duplicate name");
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("{\"totalSeats\": 10}")]
    [InlineData("{\"name\": \"   \", \"totalSeats\": 10}")]
    [InlineData("{\"name\": \"A\"}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": 0}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": -3}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": 10001}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": \"ten\"}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": 1.5}")]
    [InlineData("{\"name\": \"A\", \"totalSeats\": ")]
    [InlineData("not json at all")]
    public async Task CreateClass_InvalidInput_Returns400Problem(string json)
    {
        var response = await _client.PostAsync("/classes", Api.RawJson(json));
        await Api.AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreateClass_NameTooLong_Returns400Problem()
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name = new string('x', 101), totalSeats = 5 });
        await Api.AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetClass_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/classes/c_does_not_exist");
        await Api.AssertProblemAsync(response, HttpStatusCode.NotFound, "CLASS_NOT_FOUND");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemBody()
    {
        var response = await _client.GetAsync("/no/such/route");
        await Api.AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }
}
