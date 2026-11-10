using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Classes;

public static class ClassEndpoints
{
    public const int MaxNameLength = 100;
    public const int MinSeats = 1;
    public const int MaxSeats = 10_000;

    public static void MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes", CreateAsync);
        app.MapGet("/classes", ListAsync);
        app.MapGet("/classes/{classId}", GetAsync);
    }

    private static async Task<IResult> CreateAsync(CreateClassRequest request, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Invalid("name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            return Invalid($"name must be at most {MaxNameLength} characters.");
        }

        if (request.TotalSeats is null)
        {
            return Invalid("totalSeats is required.");
        }

        if (request.TotalSeats is < MinSeats or > MaxSeats)
        {
            return Invalid($"totalSeats must be a whole number between {MinSeats} and {MaxSeats}.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var schoolClass = new SchoolClass
        {
            Id = Ids.NewClassId(),
            Name = name,
            TotalSeats = request.TotalSeats.Value,
            CreatedAt = now,
        };
        db.Classes.Add(schoolClass);
        await db.SaveChangesAsync(ct);

        var body = ClassSummary.From(schoolClass.Id, schoolClass.Name, schoolClass.TotalSeats, 0, 0, schoolClass.CreatedAt);
        return TypedResults.Created($"/classes/{schoolClass.Id}", body);
    }

    private static async Task<IResult> ListAsync(AppDbContext db, TimeProvider time, CancellationToken ct) =>
        TypedResults.Ok(await ClassQueries.ListAsync(db, time.GetUtcNow().UtcDateTime, ct: ct));

    private static async Task<IResult> GetAsync(string classId, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var found = await ClassQueries.ListAsync(db, time.GetUtcNow().UtcDateTime, classId, ct);
        return found.Count == 1
            ? TypedResults.Ok(found[0])
            : ApiErrors.Problem(StatusCodes.Status404NotFound, ErrorCodes.ClassNotFound, "No class exists with this id.");
    }

    private static IResult Invalid(string detail) =>
        ApiErrors.Problem(StatusCodes.Status400BadRequest, ErrorCodes.ValidationError, detail);
}
