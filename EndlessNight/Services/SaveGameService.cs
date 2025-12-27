using EndlessNight.Domain;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed class SaveGameService
{
    private readonly SqliteDbContext _db;

    public SaveGameService(SqliteDbContext db)
    {
        _db = db;
    }

    public async Task<SaveGame> LoadOrCreateAsync(string playerName, string mapKey, string startingRoomKey,
        CancellationToken cancellationToken = default)
    {
        playerName = NormalizePlayerName(playerName);

        var existing = await _db.SaveGames
            .FirstOrDefaultAsync(s => s.PlayerName == playerName && s.MapKey == mapKey, cancellationToken);

        if (existing is not null)
            return existing;

        var save = new SaveGame
        {
            PlayerName = playerName,
            MapKey = mapKey,
            CurrentRoomKey = startingRoomKey,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.SaveGames.Add(save);
        await _db.SaveChangesAsync(cancellationToken);
        return save;
    }

    public async Task SaveAsync(SaveGame save, CancellationToken cancellationToken = default)
    {
        save.UpdatedUtc = DateTime.UtcNow;
        _db.SaveGames.Update(save);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizePlayerName(string playerName)
    {
        playerName = playerName.Trim();
        return string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
    }
}
