using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Features.Classes;

/// <summary>
/// Read-only seat counts. No locks: a HELD row only counts while <c>expires_at &gt; now</c>,
/// so an expired hold stops occupying its seat immediately, whether or not anything has
/// rewritten its stored status yet.
/// </summary>
public static class ClassQueries
{
    public static async Task<List<ClassSummary>> ListAsync(AppDbContext db, DateTime now, string? classId = null, CancellationToken ct = default)
    {
        var classes = db.Classes.AsNoTracking();
        if (classId is not null)
        {
            classes = classes.Where(c => c.Id == classId);
        }

        // One round-trip: correlated COUNT sub-queries per class.
        var rows = await classes
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                c.CreatedAt,
                Held = db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.HELD && h.ExpiresAt > now),
                Confirmed = db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.CONFIRMED),
            })
            .ToListAsync(ct);

        return rows.Select(r => ClassSummary.From(r.Id, r.Name, r.TotalSeats, r.Held, r.Confirmed, r.CreatedAt)).ToList();
    }
}
