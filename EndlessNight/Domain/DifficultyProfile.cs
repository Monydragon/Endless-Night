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
    /// Sort key for UI ordering. Lower comes first.
    /// </summary>
    public int SortOrder { get; set; } = 0;

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

    /// <summary>
    /// Scales hostile encounter pressure.
    /// </summary>
    public float EnemySpawnMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Scales friendly encounter pressure.
    /// </summary>
    public float NpcSpawnMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Scales sanity drain.
    /// </summary>
    public float SanityDrainMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Encounter spawning schedule knobs.
    /// 0..1 probabilities.
    /// </summary>
    public float ActorSpawnChanceOnEntry { get; set; } = 0.25f;
    public float ActorSpawnChancePerTurn { get; set; } = 0.25f;

    /// <summary>
    /// Actor movement schedule knobs.
    /// 0..1 probability. If a turn "hits", each actor gets one move attempt.
    /// </summary>
    public float ActorMoveChancePerTurn { get; set; } = 0.25f;

    /// <summary>
    /// Simple per-room caps for actors.
    /// (Keeps the system easy to reason about; can be upgraded later to depth-based or ring-based caps.)
    /// </summary>
    public int MinNpcsPerRoom { get; set; } = 0;
    public int MaxNpcsPerRoom { get; set; } = 1;
    public int MinEnemiesPerRoom { get; set; } = 0;
    public int MaxEnemiesPerRoom { get; set; } = 1;

    /// <summary>
    /// World size knobs for procedural generation.
    /// For endless mode, MaxRooms may be null.
    /// </summary>
    public int MinRooms { get; set; } = 7;
    public int MaxRooms { get; set; } = 10;

    public bool IsEndless { get; set; } = false;
}
