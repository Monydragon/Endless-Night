namespace EndlessNight.Persistence;

public sealed class SqliteOptions
{
    /// <summary>
    /// SQLite connection string (default points to a local file next to the executable).
    /// </summary>
    public required string ConnectionString { get; init; }
}
