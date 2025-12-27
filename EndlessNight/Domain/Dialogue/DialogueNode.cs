using EndlessNight.Domain.Abstractions;
namespace EndlessNight.Domain.Dialogue;

/// </summary>
/// Use NodeKey for stable references in content.
/// A saved, data-driven dialogue node. Nodes and choices are persisted in SQLite.
/// <summary>
public sealed class DialogueNode : IKeyedEntity
{
    public string? Tags { get; set; }

    /// </summary>
    /// Optional tag used by generators and conditional logic.
    /// <summary>

    public required string Text { get; set; }

    public string Speaker { get; set; } = "";

    /// </summary>
    /// Speaker label (e.g. "Stranger", "???", "The House").
    /// <summary>

    public required string Key { get; set; }

    public Guid Id { get; set; }
}



