using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Holds;

public static class HoldEndpoints
{
    public static void MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes/{classId}/holds", CreateAsync);
        app.MapGet("/holds/{holdId}", GetAsync);
        app.MapPost("/holds/{holdId}/confirm", ConfirmAsync);
        app.MapDelete("/holds/{holdId}", CancelAsync);
        app.MapGet("/me/holds", ListMineAsync);
    }

    private static async Task<IResult> CreateAsync(string classId, HttpContext http, HoldService holds, CancellationToken ct)
    {
        if (!StudentIdAccessor.TryRead(http, out var studentId, out var error))
        {
            return error;
        }

        var outcome = await holds.CreateAsync(classId, studentId, ct);
        return outcome.Error is not null
            ? outcome.Error.ToResult()
            : TypedResults.Created($"/holds/{outcome.Value!.HoldId}", outcome.Value);
    }

    private static async Task<IResult> GetAsync(string holdId, HoldService holds, CancellationToken ct)
    {
        var hold = await holds.GetAsync(holdId, ct);
        return hold is not null
            ? TypedResults.Ok(hold)
            : ApiErrors.Problem(StatusCodes.Status404NotFound, ErrorCodes.HoldNotFound, "No application exists with this id.");
    }

    private static async Task<IResult> ConfirmAsync(string holdId, HttpContext http, HoldService holds, CancellationToken ct)
    {
        if (!StudentIdAccessor.TryRead(http, out var studentId, out var error))
        {
            return error;
        }

        var outcome = await holds.ConfirmAsync(holdId, studentId, ct);
        return outcome.Error?.ToResult() ?? TypedResults.Ok(outcome.Value);
    }

    private static async Task<IResult> CancelAsync(string holdId, HttpContext http, HoldService holds, CancellationToken ct)
    {
        if (!StudentIdAccessor.TryRead(http, out var studentId, out var error))
        {
            return error;
        }

        var outcome = await holds.CancelAsync(holdId, studentId, ct);
        return outcome.Error?.ToResult() ?? TypedResults.NoContent();
    }

    private static async Task<IResult> ListMineAsync(HttpContext http, HoldService holds, CancellationToken ct)
    {
        if (!StudentIdAccessor.TryRead(http, out var studentId, out var error))
        {
            return error;
        }

        return TypedResults.Ok(await holds.ListForStudentAsync(studentId, ct));
    }
}
