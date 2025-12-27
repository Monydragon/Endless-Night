using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Configurable difficulty profile stored in SQLite.
/// This is intended to be data-driven so balancing doesn't require code changes.
/// </summary>
public sealed class DifficultyProfile : IEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key used by saves/config (e.g. "casual", "normal", "hard", "endless").
    /// </summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Optional short description shown in menus.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Starting stats (0..100).
    /// </summary>
    public int StartingHealth { get; set; } = 100;
    public int StartingSanity { get; set; } = 100;

    /// <summary>
    /// Difficulty tuning knobs.
    /// </summary>
    public float LootMultiplier { get; set; } = 1.0f;
    public float EnemySpawnMultiplier { get; set; } = 1.0f;
    public float SanityDrainMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// World size knobs for procedural generation.
    /// For endless mode, MaxRooms may be null.
    /// </summary>
    public int MinRooms { get; set; } = 7;
    public int MaxRooms { get; set; } = 10;

    public bool IsEndless { get; set; } = false;
}

