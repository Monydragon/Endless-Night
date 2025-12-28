using EndlessNight.Domain;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

/// <summary>
/// Simple DB-backed settings surface.
/// Values are stored as strings for forward compatibility.
/// </summary>
public sealed class SettingsService
{
    private readonly SqliteDbContext _db;

    public SettingsService(SqliteDbContext db)
    {
        _db = db;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var s = await _db.GameSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (s is null) return defaultValue;
        return bool.TryParse(s.Value, out var b) ? b : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default)
    {
        var s = await _db.GameSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (s is null) return defaultValue;
        return int.TryParse(s.Value, out var v) ? v : defaultValue;
    }

    public async Task<string> GetStringAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default)
    {
        var s = await _db.GameSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        return s?.Value ?? defaultValue;
    }

    public async Task SetBoolAsync(string key, bool value, CancellationToken cancellationToken = default)
        => await SetStringAsync(key, value ? "true" : "false", cancellationToken);

    public async Task SetIntAsync(string key, int value, CancellationToken cancellationToken = default)
        => await SetStringAsync(key, value.ToString(), cancellationToken);

    public async Task SetStringAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        key = (key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key is required", nameof(key));

        var existing = await _db.GameSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            _db.GameSettings.Add(new GameSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value ?? string.Empty,
                UpdatedUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value ?? string.Empty;
            existing.UpdatedUtc = DateTime.UtcNow;
            _db.GameSettings.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultAsync("debug.enabled", "false", cancellationToken);
        await EnsureDefaultAsync("ui.sanitySlip.enabled", "true", cancellationToken);
        await EnsureDefaultAsync("ui.sanitySlip.threshold", "35", cancellationToken);
        await EnsureDefaultAsync("metaMemory.mode", "subtle", cancellationToken);
        await EnsureDefaultAsync("audio.voice.provider", "offline", cancellationToken);
    }

    private async Task EnsureDefaultAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await _db.GameSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is not null) return;

        _db.GameSettings.Add(new GameSetting
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = value,
            UpdatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MetaMemoryPreference> GetMetaMemoryPreferenceAsync(CancellationToken cancellationToken = default)
    {
        var raw = await GetStringAsync("metaMemory.mode", "subtle", cancellationToken);
        return MetaMemoryPreference.FromString(raw);
    }

    public async Task SetMetaMemoryPreferenceAsync(MetaMemoryPreference preference, CancellationToken cancellationToken = default)
        => await SetStringAsync("metaMemory.mode", preference.ToStorageString(), cancellationToken);

    public async Task<string> GetVoiceProviderAsync(CancellationToken cancellationToken = default)
        => await GetStringAsync("audio.voice.provider", "offline", cancellationToken);

    public async Task SetVoiceProviderAsync(string providerKey, CancellationToken cancellationToken = default)
        => await SetStringAsync("audio.voice.provider", providerKey, cancellationToken);
}
