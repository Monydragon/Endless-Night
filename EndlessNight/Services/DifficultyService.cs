using EndlessNight.Domain;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed class DifficultyService
{
    private readonly SqliteDbContext _db;

    public DifficultyService(SqliteDbContext db)
    {
        _db = db;
    }

    public Task<List<DifficultyProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        => _db.DifficultyProfiles
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<DifficultyProfile> GetOrDefaultAsync(string? key, CancellationToken cancellationToken = default)
    {
        key = (key ?? string.Empty).Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(key))
        {
            var existing = await _db.DifficultyProfiles.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (existing is not null)
                return existing;
        }

        // Always ensure we have a working fallback.
        var normal = await _db.DifficultyProfiles.FirstOrDefaultAsync(x => x.Key == "normal", cancellationToken);
        if (normal is not null)
            return normal;

        // If DB isn't seeded for some reason, return a sensible default.
        return new DifficultyProfile
        {
            Id = Guid.Empty,
            Key = "normal",
            Name = "Normal",
            Description = "The intended experience.",
            StartingHealth = 100,
            StartingSanity = 100,
            LootMultiplier = 1.0f,
            EnemySpawnMultiplier = 1.0f,
            SanityDrainMultiplier = 1.0f,
            MinRooms = 7,
            MaxRooms = 10,
            IsEndless = false
        };
    }
}
