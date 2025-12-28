using System.IO;
using Microsoft.EntityFrameworkCore;
using EndlessNight.Persistence;

namespace EndlessNight.Persistence;

public static class SqliteDbContextFactory
{
    // Alias overload to maintain compatibility with older call sites.
    public static SqliteDbContext Create(string connectionString, bool? resetOnModelMismatch)
        => Create(connectionString, resetOnMismatch: (resetOnModelMismatch ?? false));

    public static bool TryResetDatabase(string connectionString)
    {
        try
        {
            // Expect connection string like: Data Source=path\to\file.db;...
            var parts = connectionString.Split(';');
            var dataSourcePart = parts.FirstOrDefault(p => p.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(dataSourcePart))
                return false;

            var path = dataSourcePart.Substring(dataSourcePart.IndexOf('=') + 1).Trim();
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static SqliteDbContext Create(string connectionString, bool resetOnMismatch = false)
    {
        var options = new DbContextOptionsBuilder<SqliteDbContext>()
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        var db = new SqliteDbContext(options);

        // Ensure the file and basic schema exist.
        db.Database.EnsureCreated();

        if (resetOnMismatch)
        {
            try
            {
                // Schema probe: hit a column that exists only in newer builds.
                // If the DB is stale, SQLite will throw "no such column".
                db.Database.ExecuteSqlRaw("SELECT AutoSpeakOnEnter FROM ActorInstances LIMIT 1;");
            }
            catch
            {
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            }
        }

        return db;
    }
}
