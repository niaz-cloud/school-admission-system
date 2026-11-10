namespace SchoolAdmission.Api.Infrastructure;

/// <summary>Runtime settings read from environment variables (via configuration).</summary>
public sealed class AdmissionOptions
{
    public const int DefaultHoldDurationSeconds = 600;
    public const int DefaultSweeperIntervalSeconds = 30;

    public int HoldDurationSeconds { get; init; } = DefaultHoldDurationSeconds;
    public int SweeperIntervalSeconds { get; init; } = DefaultSweeperIntervalSeconds;

    public static AdmissionOptions FromConfiguration(IConfiguration configuration) => new()
    {
        HoldDurationSeconds = ReadPositive(configuration, "HOLD_DURATION_SECONDS", DefaultHoldDurationSeconds),
        SweeperIntervalSeconds = ReadPositive(configuration, "SWEEPER_INTERVAL_SECONDS", DefaultSweeperIntervalSeconds),
    };

    private static int ReadPositive(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
}
