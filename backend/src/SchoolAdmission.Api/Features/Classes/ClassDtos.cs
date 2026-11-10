namespace SchoolAdmission.Api.Features.Classes;

/// <summary>Fields are nullable so a missing field is reported as a validation error instead of defaulting to 0/null.</summary>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);

public sealed record ClassSummary(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats,
    DateTime CreatedAt)
{
    public static ClassSummary From(string id, string name, int totalSeats, int held, int confirmed, DateTime createdAt) =>
        new(id, name, totalSeats, held, confirmed, Math.Max(0, totalSeats - held - confirmed), createdAt);
}
