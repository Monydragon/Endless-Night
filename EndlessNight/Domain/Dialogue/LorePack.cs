using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Dialogue;

/// <summary>
/// A lore/style pack used by procedural dialogue and procedural loot.
/// </summary>
public sealed class LorePack : IKeyedEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key (e.g. "cosmic-horror", "lovecraft", "zork", "undertale").
    /// </summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Optional style tags (stored as a delimited string for now).
    /// Example: "cosmic;horror;parser"
    /// </summary>
    public string? StyleTags { get; set; }
}

