using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>Either a value or an <see cref="ApiError"/>.</summary>
public readonly record struct Outcome<T>(T? Value, ApiError? Error)
{
    public static implicit operator Outcome<T>(T value) => new(value, null);
    public static implicit operator Outcome<T>(ApiError error) => new(default, error);
}

/// <summary>
/// All writes to a class's holds happen inside a transaction that first locks that class's row
/// with <c>SELECT … FOR UPDATE</c>. That serialises hold mutations per class in PostgreSQL, so the
/// count-then-insert in <see cref="CreateAsync"/> can never over-allocate. Because the lock lives
/// in the database, this holds across threads and across separate backend processes alike.
/// Lock order is always class row, then hold row, which rules out deadlocks between writers.
/// </summary>
public sealed class HoldService(AppDbContext db, TimeProvider time, AdmissionOptions options)
{
    public async Task<Outcome<HoldResponse>> CreateAsync(string classId, string studentId, CancellationToken ct)
    {
        try
        {
            return await CreateCoreAsync(classId, studentId, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Second line of defence: the partial unique index caught a duplicate active hold.
            db.ChangeTracker.Clear();
            return AlreadyApplied(await ActiveHoldIdAsync(classId, studentId, ct));
        }
    }

    private async Task<Outcome<HoldResponse>> CreateCoreAsync(string classId, string studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var schoolClass = await LockClassAsync(classId, ct);
        if (schoolClass is null)
        {
            return ClassNotFound();
        }

        // Read the clock only after the lock is held: a request that queued behind others must
        // judge expiry by the moment it actually acts.
        var now = Now();

        // Retire this class's expired holds so the partial unique index (HELD/CONFIRMED) stays truthful.
        await ExpireOverdueAsync(classId, now, ct);

        var existingId = await ActiveHoldIdAsync(classId, studentId, ct);
        if (existingId is not null)
        {
            return AlreadyApplied(existingId);
        }

        var used = await db.Holds.CountAsync(
            h => h.ClassId == classId && (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED), ct);
        if (used >= schoolClass.TotalSeats)
        {
            return new ApiError(StatusCodes.Status409Conflict, ErrorCodes.SeatsFull, "No seats are available in this class.");
        }

        var hold = new Hold
        {
            Id = Ids.NewHoldId(),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.HELD,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddSeconds(options.HoldDurationSeconds),
        };
        db.Holds.Add(hold);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return HoldResponse.From(hold, schoolClass.Name, now);
    }

    public async Task<Outcome<HoldResponse>> ConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        var classId = await ClassIdOfHoldAsync(holdId, ct);
        if (classId is null)
        {
            return HoldNotFound();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var schoolClass = await LockClassAsync(classId, ct);
        var hold = await LockHoldAsync(holdId, ct);
        if (schoolClass is null || hold is null)
        {
            return HoldNotFound();
        }

        var now = Now();
        if (hold.StudentId != studentId)
        {
            return NotOwner();
        }

        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.CONFIRMED:
                // Idempotent: confirming again changes nothing and counts nothing.
                return HoldResponse.From(hold, schoolClass.Name, now);
            case HoldStatus.CANCELLED:
                return new ApiError(StatusCodes.Status409Conflict, ErrorCodes.HoldCancelled, "This application was cancelled.");
            case HoldStatus.EXPIRED:
                await MarkExpiredAsync(hold, now, tx, ct);
                return Expired();
        }

        hold.Status = HoldStatus.CONFIRMED;
        hold.ConfirmedAt = now;
        hold.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return HoldResponse.From(hold, schoolClass.Name, now);
    }

    public async Task<Outcome<bool>> CancelAsync(string holdId, string studentId, CancellationToken ct)
    {
        var classId = await ClassIdOfHoldAsync(holdId, ct);
        if (classId is null)
        {
            return HoldNotFound();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var schoolClass = await LockClassAsync(classId, ct);
        var hold = await LockHoldAsync(holdId, ct);
        if (schoolClass is null || hold is null)
        {
            return HoldNotFound();
        }

        var now = Now();
        if (hold.StudentId != studentId)
        {
            return NotOwner();
        }

        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.CANCELLED:
                return true; // DELETE is idempotent.
            case HoldStatus.CONFIRMED:
                return new ApiError(StatusCodes.Status409Conflict, ErrorCodes.HoldAlreadyConfirmed,
                    "This admission is already confirmed and can no longer be cancelled.");
            case HoldStatus.EXPIRED:
                await MarkExpiredAsync(hold, now, tx, ct);
                return Expired();
        }

        hold.Status = HoldStatus.CANCELLED;
        hold.CancelledAt = now;
        hold.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<HoldResponse?> GetAsync(string holdId, CancellationToken ct)
    {
        var row = await db.Holds.AsNoTracking()
            .Where(h => h.Id == holdId)
            .Join(db.Classes, h => h.ClassId, c => c.Id, (h, c) => new { Hold = h, ClassName = c.Name })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : HoldResponse.From(row.Hold, row.ClassName, Now());
    }

    public async Task<List<HoldResponse>> ListForStudentAsync(string studentId, CancellationToken ct)
    {
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Join(db.Classes, h => h.ClassId, c => c.Id, (h, c) => new { Hold = h, ClassName = c.Name })
            .ToListAsync(ct);
        var now = Now();
        return rows.Select(r => HoldResponse.From(r.Hold, r.ClassName, now)).ToList();
    }

    // ---- helpers ------------------------------------------------------------------------

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    // ToListAsync rather than FirstOrDefaultAsync: composing LINQ on top of the raw query would
    // make EF wrap it in a sub-query, which PostgreSQL rejects for FOR UPDATE.
    private async Task<SchoolClass?> LockClassAsync(string classId, CancellationToken ct) =>
        (await db.Classes.FromSqlInterpolated($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    private async Task<Hold?> LockHoldAsync(string holdId, CancellationToken ct) =>
        (await db.Holds.FromSqlInterpolated($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();

    private Task<string?> ClassIdOfHoldAsync(string holdId, CancellationToken ct) =>
        db.Holds.AsNoTracking().Where(h => h.Id == holdId).Select(h => h.ClassId).FirstOrDefaultAsync(ct);

    private Task<string?> ActiveHoldIdAsync(string classId, string studentId, CancellationToken ct) =>
        db.Holds.AsNoTracking()
            .Where(h => h.ClassId == classId && h.StudentId == studentId
                && (h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED))
            .Select(h => h.Id)
            .FirstOrDefaultAsync(ct);

    private Task<int> ExpireOverdueAsync(string classId, DateTime now, CancellationToken ct) =>
        db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HoldStatus.EXPIRED)
                .SetProperty(h => h.UpdatedAt, now), ct);

    private async Task MarkExpiredAsync(Hold hold, DateTime now, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        if (hold.Status == HoldStatus.HELD)
        {
            hold.Status = HoldStatus.EXPIRED;
            hold.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static ApiError ClassNotFound() =>
        new(StatusCodes.Status404NotFound, ErrorCodes.ClassNotFound, "No class exists with this id.");

    private static ApiError HoldNotFound() =>
        new(StatusCodes.Status404NotFound, ErrorCodes.HoldNotFound, "No application exists with this id.");

    private static ApiError NotOwner() =>
        new(StatusCodes.Status403Forbidden, ErrorCodes.NotHoldOwner, "This application belongs to a different student.");

    private static ApiError Expired() =>
        new(StatusCodes.Status409Conflict, ErrorCodes.HoldExpired, "The hold on this seat has expired.");

    private static ApiError AlreadyApplied(string? existingHoldId) => new(
        StatusCodes.Status409Conflict,
        ErrorCodes.AlreadyApplied,
        "This student already has an active or confirmed application for this class.",
        new Dictionary<string, object?> { ["holdId"] = existingHoldId });
}
