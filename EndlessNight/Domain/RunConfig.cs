using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Per-run configuration for future expansion (lore packs, endless generation toggles, etc.).
/// This is separated from <see cref="RunState"/> so we can add options without bloating saves.
/// </summary>
public sealed class RunConfig : IEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    /// <summary>
    /// Difficulty key duplicated here for convenience when querying configs.
    /// RunState.DifficultyKey remains the authoritative value.
    /// </summary>
    public string DifficultyKey { get; set; } = "normal";

    /// <summary>
    /// Lore/content packs enabled for this run (e.g. "lovecraft", "zork", "undertale").
    /// Stored as JSON string list.
    /// </summary>
    public List<string> EnabledLorePacks { get; set; } = new();

    /// <summary>
    /// If true, the run may generate rooms indefinitely.
    /// </summary>
    public bool EndlessEnabled { get; set; }

    /// <summary>
    /// Optional cap for runs that are "endless" but still want a limit.
    /// Null means unlimited.
    /// </summary>
    public int? MaxRooms { get; set; }

    /// <summary>
    /// Reserved seed offset for procedural dialogue and encounter systems.
    /// </summary>
    public int DialogueSeedOffset { get; set; } = 17;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

