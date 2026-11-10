using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Housekeeping only: rewrites the stored status of overdue HELD rows to EXPIRED. Seat counts
/// never depend on this (they compare <c>expires_at</c> with the current time), so a slow or
/// stopped sweeper cannot keep a seat occupied.
///
/// It takes the same per-class row lock as every other writer, which keeps one lock order for
/// all writers and so cannot deadlock with them. Running it in several instances is harmless:
/// the update is idempotent.
/// </summary>
public sealed class ExpiredHoldSweeper(
    IServiceScopeFactory scopes,
    TimeProvider time,
    AdmissionOptions options,
    ILogger<ExpiredHoldSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.SweeperIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var expired = await SweepOnceAsync(stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Marked {Count} overdue holds as EXPIRED", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never crash the host over housekeeping.
                logger.LogWarning(ex, "Expired-hold sweep failed; will retry next tick");
            }
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = time.GetUtcNow().UtcDateTime;
        var classIds = await db.Holds.AsNoTracking()
            .Where(h => h.Status == HoldStatus.HELD && h.ExpiresAt <= now)
            .Select(h => h.ClassId)
            .Distinct()
            .ToListAsync(ct);

        var total = 0;
        foreach (var classId in classIds)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM classes WHERE id = {classId} FOR UPDATE", ct);
            var lockedNow = time.GetUtcNow().UtcDateTime;
            total += await db.Holds
                .Where(h => h.ClassId == classId && h.Status == HoldStatus.HELD && h.ExpiresAt <= lockedNow)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.Status, HoldStatus.EXPIRED)
                    .SetProperty(h => h.UpdatedAt, lockedNow), ct);
            await tx.CommitAsync(ct);
        }

        return total;
    }
}
