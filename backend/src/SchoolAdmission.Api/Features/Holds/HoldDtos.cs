using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// One shape for every hold response. It is a superset of the fields each endpoint's contract
/// requires. <see cref="Status"/> is always the effective status; <see cref="ServerTime"/> lets
/// the UI correct its countdown for client clock skew.
/// </summary>
public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string StudentId,
    HoldStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    DateTime ServerTime)
{
    public static HoldResponse From(Hold hold, string className, DateTime now) => new(
        hold.Id,
        hold.ClassId,
        className,
        hold.StudentId,
        hold.EffectiveStatus(now),
        hold.ExpiresAt,
        hold.CreatedAt,
        hold.ConfirmedAt,
        hold.CancelledAt,
        now);
}
