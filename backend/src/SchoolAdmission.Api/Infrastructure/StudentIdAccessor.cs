using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>Reads and validates the <c>X-Student-Id</c> header (17-digit birth registration number).</summary>
public static partial class StudentIdAccessor
{
    public const string HeaderName = "X-Student-Id";

    // [0-9], not \d: in .NET, \d matches every Unicode digit (Bengali ০-৯ included).
    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex Pattern();

    public static bool TryRead(HttpContext context, [NotNullWhen(true)] out string? studentId, [NotNullWhen(false)] out IResult? error)
    {
        var raw = context.Request.Headers[HeaderName].ToString().Trim();
        if (raw.Length == 0)
        {
            studentId = null;
            error = ApiErrors.Problem(StatusCodes.Status400BadRequest, ErrorCodes.StudentIdRequired,
                $"The {HeaderName} header (17-digit birth registration number) is required.");
            return false;
        }

        if (!Pattern().IsMatch(raw))
        {
            studentId = null;
            error = ApiErrors.Problem(StatusCodes.Status400BadRequest, ErrorCodes.StudentIdInvalid,
                $"The {HeaderName} header must be exactly 17 digits.");
            return false;
        }

        studentId = raw;
        error = null;
        return true;
    }
}
