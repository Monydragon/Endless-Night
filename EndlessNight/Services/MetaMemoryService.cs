using EndlessNight.Domain;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

/// <summary>
/// Reads world flags + preferences to determine what meta-memory should influence the current run.
/// </summary>
public sealed class MetaMemoryService
{
    private readonly SqliteDbContext _db;
    private readonly SettingsService _settings;

    public MetaMemoryService(SqliteDbContext db)
    {
        _db = db;
        _settings = new SettingsService(db);
    }

    public async Task<MetaMemoryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        var pref = await _settings.GetMetaMemoryPreferenceAsync(cancellationToken);
        var flags = await _db.WorldFlags
            .AsNoTracking()
            .ToDictionaryAsync(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return new MetaMemoryState(pref.Mode, flags);
    }
}

