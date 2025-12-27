using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Dialogue;

/// <summary>
/// A small vocabulary item used by the non-LLM procedural dialogue composer.
/// </summary>
public sealed class LoreWord : IKeyedEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key (e.g. "lovecraft.fear.ink")
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Word or short phrase.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Category like "fear".
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Optional lore pack key required.
    /// </summary>
    public string? PackKey { get; set; }

    /// <summary>
    /// Relative selection weight.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>
    /// Optional sanity bounds.
    /// </summary>
    public int? MinSanity { get; set; }
    public int? MaxSanity { get; set; }
}

