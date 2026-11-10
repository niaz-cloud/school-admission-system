using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Turns every unhandled exception into the standard error body. Malformed JSON and
/// binding failures become 400 VALIDATION_ERROR; anything else becomes 500 INTERNAL_ERROR
/// without leaking a stack trace.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        IResult result;
        if (exception is BadHttpRequestException or JsonException)
        {
            logger.LogInformation("Rejected bad request: {Message}", exception.Message);
            result = ApiErrors.Problem(
                StatusCodes.Status400BadRequest,
                ErrorCodes.ValidationError,
                "The request body is missing, malformed, or has fields of the wrong type.");
        }
        else
        {
            logger.LogError(exception, "Unhandled exception");
            result = ApiErrors.Problem(
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "An unexpected error occurred on the server.");
        }

        await result.ExecuteAsync(httpContext);
        return true;
    }
}
