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

            // Lightweight schema patching for older DB files.
            // We don't use EF migrations in this repo, so we apply a tiny in-place upgrade for known additions.
            ApplySchemaPatches(db);

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

            ApplySchemaPatches(db2);

            return db2;
        }
    }

    private static void ApplySchemaPatches(SqliteDbContext db)
    {
        // Only patch what we know about; keep it safe and idempotent.
        try
        {
            PatchDifficultyProfiles(db);
            PatchRoomInstances(db);
        }
        catch
        {
            // Never block startup due to a best-effort patch.
        }
    }

    private static void PatchDifficultyProfiles(SqliteDbContext db)
    {
        // Ensure new actor-tuning columns exist.
        // SQLite supports ADD COLUMN but not IF NOT EXISTS, so we inspect PRAGMA table_info.
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('DifficultyProfiles');";
            if (cmd.Connection!.State != System.Data.ConnectionState.Open)
                cmd.Connection.Open();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                var name = reader.GetString(1);
                existing.Add(name);
            }
        }

        // Note: EF uses property names as column names by default.
        var alters = new List<string>();

        void AddColumn(string colName, string sqliteType, string defaultSql)
        {
            if (!existing.Contains(colName))
                alters.Add($"ALTER TABLE DifficultyProfiles ADD COLUMN {colName} {sqliteType} NOT NULL DEFAULT {defaultSql};");
        }

        // New UI ordering
        AddColumn("SortOrder", "INTEGER", "0");

        // Actor tuning
        AddColumn("NpcSpawnMultiplier", "REAL", "1.0");
        AddColumn("ActorSpawnChanceOnEntry", "REAL", "0.25");
        AddColumn("ActorSpawnChancePerTurn", "REAL", "0.25");
        AddColumn("ActorMoveChancePerTurn", "REAL", "0.25");
        AddColumn("MinNpcsPerRoom", "INTEGER", "0");
        AddColumn("MaxNpcsPerRoom", "INTEGER", "1");
        AddColumn("MinEnemiesPerRoom", "INTEGER", "0");
        AddColumn("MaxEnemiesPerRoom", "INTEGER", "1");

        // New trap/pacify tuning
        AddColumn("TrapDisarmSanityCost", "INTEGER", "5");
        AddColumn("PacifyBaseSanityCost", "INTEGER", "8");
        AddColumn("PacifyCostMultiplier", "REAL", "1.0");

        if (alters.Count == 0)
            return;

        foreach (var sql in alters)
            db.Database.ExecuteSqlRaw(sql);
    }

    private static void PatchRoomInstances(SqliteDbContext db)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('RoomInstances');";
            if (cmd.Connection!.State != System.Data.ConnectionState.Open)
                cmd.Connection.Open();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                existing.Add(name);
            }
        }

        var alters = new List<string>();

        void AddColumn(string colName, string sqliteType, string defaultSql)
        {
            if (!existing.Contains(colName))
                alters.Add($"ALTER TABLE RoomInstances ADD COLUMN {colName} {sqliteType} NOT NULL DEFAULT {defaultSql};");
        }

        AddColumn("IsCleared", "INTEGER", "0");

        if (alters.Count == 0)
            return;

        foreach (var sql in alters)
            db.Database.ExecuteSqlRaw(sql);
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
