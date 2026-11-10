using Microsoft.AspNetCore.WebUtilities;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>Machine-readable error codes returned in the <c>code</c> field of every error body.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string StudentIdRequired = "STUDENT_ID_REQUIRED";
    public const string StudentIdInvalid = "STUDENT_ID_INVALID";
    public const string SeatsFull = "SEATS_FULL";
    public const string AlreadyApplied = "ALREADY_APPLIED";
    public const string NotHoldOwner = "NOT_HOLD_OWNER";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldAlreadyConfirmed = "HOLD_ALREADY_CONFIRMED";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string InternalError = "INTERNAL_ERROR";
}

/// <summary>An error produced by the domain layer; converted to an RFC 7807 body at the HTTP edge.</summary>
public sealed record ApiError(int Status, string Code, string Detail, IReadOnlyDictionary<string, object?>? Extras = null)
{
    public IResult ToResult() => ApiErrors.Problem(Status, Code, Detail, Extras);
}

public static class ApiErrors
{
    /// <summary>Builds the single error shape used by every non-2xx response.</summary>
    public static IResult Problem(int status, string code, string detail, IReadOnlyDictionary<string, object?>? extras = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (extras is not null)
        {
            foreach (var (key, value) in extras)
            {
                extensions[key] = value;
            }
        }

        return TypedResults.Problem(
            detail: detail,
            statusCode: status,
            title: ReasonPhrases.GetReasonPhrase(status),
            type: "about:blank",
            extensions: extensions);
    }
}
