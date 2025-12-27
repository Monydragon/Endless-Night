using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Dialogue;

/// <summary>
/// A small reusable piece of dialogue text used by a procedural (non-LLM) composer.
/// </summary>
public sealed class DialogueSnippet : IKeyedEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key (e.g. "cosmic.opening.001").
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The snippet text. Can contain simple tokens like {player}, {room}, {fearWord}.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Optional: semicolon-delimited tags (e.g. "opening;whisper;room").
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Relative weight used for random selection.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>
    /// Optional: lore pack key required for this snippet to be eligible.
    /// If null, snippet is pack-agnostic.
    /// </summary>
    public string? PackKey { get; set; }

    /// <summary>
    /// Optional sanity bounds
    /// </summary>
    public int? MinSanity { get; set; }
    public int? MaxSanity { get; set; }

    /// <summary>
    /// Optional morality bounds
    /// </summary>
    public int? MinMorality { get; set; }
    public int? MaxMorality { get; set; }

    /// <summary>
    /// Optional: constrain to a specific actor disposition ("Friendly", "Hostile", "Unknown")
    /// </summary>
    public string? RequiredDisposition { get; set; }

    /// <summary>
    /// Optional: assembly role (e.g. opening/middle/closing)
    /// </summary>
    public string? Role { get; set; }
}

