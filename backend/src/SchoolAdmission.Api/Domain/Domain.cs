namespace SchoolAdmission.Api.Domain;

/// <summary>Stored as text. Member names are the exact UPPERCASE strings in the API contract.</summary>
public enum HoldStatus
{
    HELD,
    CONFIRMED,
    EXPIRED,
    CANCELLED,
}

/// <summary>One class+shift with a fixed number of seats (e.g. "Class 6 - Morning Shift").</summary>
public sealed class SchoolClass
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>A student's application for a seat in a class.</summary>
public sealed class Hold
{
    public required string Id { get; init; }
    public required string ClassId { get; init; }
    public required string StudentId { get; init; }
    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The status the outside world sees: a HELD row whose time has run out is EXPIRED even if
    /// nothing has rewritten the stored value yet. Seat availability is computed the same way,
    /// so a seat comes back the instant <see cref="ExpiresAt"/> passes, with no timer involved.
    /// </summary>
    public HoldStatus EffectiveStatus(DateTime now) =>
        Status == HoldStatus.HELD && ExpiresAt <= now ? HoldStatus.EXPIRED : Status;
}

public static class Ids
{
    public static string NewClassId() => "c_" + Guid.NewGuid().ToString("N");
    public static string NewHoldId() => "h_" + Guid.NewGuid().ToString("N");
}
