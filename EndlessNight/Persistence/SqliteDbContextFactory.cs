using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EndlessNight.Persistence;

public static class SqliteDbContextFactory
{
    public static SqliteDbContext Create(IConfiguration configuration)
    {
        var section = configuration.GetSection("Sqlite");

        // Default: local file next to the executable.
        var connectionString =
            section["ConnectionString"] ??
            configuration["Sqlite__ConnectionString"] ??
            "Data Source=endless-night.db";

        var resetOnMismatch =
            bool.TryParse(configuration["Sqlite__ResetOnModelMismatch"], out var parsed) && parsed;

        return Create(connectionString, resetOnMismatch);
    }

    public static SqliteDbContext Create(string connectionString, bool resetOnModelMismatch)
    {
        var options = new DbContextOptionsBuilder<SqliteDbContext>()
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging(false)
            .Options;

        var db = new SqliteDbContext(options);

        try
        {
            db.Database.EnsureCreated();
            return db;
        }
        catch (Exception) when (resetOnModelMismatch)
        {
            try
            {
                db.Dispose();
            }
            catch
            {
                // ignore
            }

            if (!TryDeleteSqliteFile(connectionString))
                throw;

            // Recreate after deleting old file.
            var db2 = new SqliteDbContext(options);
            db2.Database.EnsureCreated();
            return db2;
        }
    }

    public static bool TryResetDatabase(string connectionString)
    {
        return TryDeleteSqliteFile(connectionString);
    }

    private static bool TryDeleteSqliteFile(string connectionString)
    {
        var path = TryExtractDataSourcePath(connectionString);
        if (string.IsNullOrWhiteSpace(path))
            return false;

        path = Path.GetFullPath(path);

        // Ensure SQLite doesn't keep pooled connections around.
        try
        {
            SqliteDbContext.ClearAllPools();
        }
        catch
        {
            // ignore
        }

        if (!File.Exists(path))
            return true;

        return TryDeleteWithRetries(path, attempts: 5, delayMs: 150);
    }

    private static bool TryDeleteWithRetries(string dbPath, int attempts, int delayMs)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                File.Delete(dbPath);

                // Clear WAL/SHM leftovers.
                TryDeleteIfExists(dbPath + "-wal");
                TryDeleteIfExists(dbPath + "-shm");

                return true;
            }
            catch (IOException) when (i < attempts - 1)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException) when (i < attempts - 1)
            {
                Thread.Sleep(delayMs);
            }
        }

        return false;
    }

    private static string? TryExtractDataSourcePath(string connectionString)
    {
        // Very small parser for the common pattern: "Data Source=...".
        // If the connection string is more complex, we don't try to reset.
        const string prefix = "Data Source=";
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return part.Substring(prefix.Length).Trim().Trim('"');
        }

        return null;
    }

    private static void TryDeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
