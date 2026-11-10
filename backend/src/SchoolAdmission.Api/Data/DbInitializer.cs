using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Applies migrations at startup. Waits for the database to accept connections (it may still be
/// booting under docker compose), then migrates while holding a PostgreSQL advisory lock so that
/// two backend instances starting at the same moment cannot race each other.
/// </summary>
public static class DbInitializer
{
    private const long MigrationLockKey = 424242;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        await WaitForDatabaseAsync(db, logger, cancellationToken);

        // The advisory lock is session-scoped, so the lock, the migration and the unlock
        // must all run on this one explicitly opened connection.
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        logger.LogInformation("Database is migrated and ready");
    }

    private static async Task WaitForDatabaseAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + MaxWait;
        while (true)
        {
            try
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                // The server is up but the database does not exist yet: create it, then retry.
                await CreateDatabaseAsync(db, logger, cancellationToken);
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException && DateTime.UtcNow < deadline)
            {
                logger.LogWarning("Database not reachable yet ({Message}); retrying", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static async Task CreateDatabaseAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Database does not exist; creating it");
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateDatabase)
        {
            // Another instance created it first. That is fine.
        }
    }
}
