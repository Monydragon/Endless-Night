using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Dialogue;

/// <summary>
/// A choice from one dialogue node to another. Stores deterministic consequences.
/// </summary>
public sealed class DialogueChoice : IEntity
{
    public Guid Id { get; set; }

    public required string FromNodeKey { get; set; }

    public required string Text { get; set; }

    /// <summary>
    /// Next node key, or null to end the conversation.
    /// </summary>
    public string? ToNodeKey { get; set; }

    /// <summary>
    /// Optional requirement gates.
    /// </summary>
    public int? RequireMinMorality { get; set; }
    public int? RequireMaxMorality { get; set; }
    public int? RequireMinSanity { get; set; }

    /// <summary>
    /// Deterministic consequences.
    /// </summary>
    public int SanityDelta { get; set; }
    public int HealthDelta { get; set; }
    public int MoralityDelta { get; set; }

    /// <summary>
    /// Optional: reveal/change how the player perceives the actor.
    /// </summary>
    public EndlessNight.Domain.ActorDisposition? RevealDisposition { get; set; }

    /// <summary>
    /// Optional: pacify the actor (enemy-only).
    /// </summary>
    public bool PacifyTarget { get; set; }

    /// <summary>
    /// Optional: grant an item to the player when this choice is taken.
    /// </summary>
    public string? GrantItemKey { get; set; }

    /// <summary>
    /// Quantity for GrantItemKey. Defaults to 1 when GrantItemKey is set.
    /// </summary>
    public int GrantItemQuantity { get; set; }
}
