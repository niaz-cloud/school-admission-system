using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration ------------------------------------------------------------------
builder.Services.AddSingleton(sp => AdmissionOptions.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(TimeProvider.System);

// Resolved lazily from IConfiguration so tests (and env vars) can override the connection string.
// Note: no EnableRetryOnFailure — it is incompatible with the explicit transactions used for locking.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

// ---- JSON: camelCase properties, UPPERCASE enum names, UTC "Z" timestamps -------------
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
});

// ---- errors: every non-2xx response is RFC 7807 + "code" ------------------------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Binding failures must throw (and so reach GlobalExceptionHandler) in every environment,
// not only Development; otherwise Production returns a bare 400 with no body.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// ---- CORS -----------------------------------------------------------------------------
const string CorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    var origins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "*")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length == 0 || origins.Contains("*"))
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(origins);
    }

    policy.WithHeaders("X-Student-Id", "Content-Type")
        .WithMethods("GET", "POST", "DELETE")
        .WithExposedHeaders("Location");
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    var (code, detail) = status switch
    {
        StatusCodes.Status404NotFound => (ErrorCodes.NotFound, "The requested resource does not exist."),
        StatusCodes.Status405MethodNotAllowed => (ErrorCodes.MethodNotAllowed, "This HTTP method is not allowed on this path."),
        _ => (ErrorCodes.InternalError, "The request could not be completed."),
    };
    await ApiErrors.Problem(status, code, detail).ExecuteAsync(context.HttpContext);
});
app.UseCors(CorsPolicy);

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));

await DbInitializer.InitializeAsync(app.Services);

app.Run();

/// <summary>Exposed so the integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
