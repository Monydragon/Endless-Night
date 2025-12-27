using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain.Dialogue;

/// <summary>
/// Run-scoped dialogue state for a specific actor (what node they're on, flags).
/// </summary>
public sealed class RunDialogueState : IRunScoped
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public required Guid ActorId { get; set; }

    /// <summary>
    /// Current node key for this actor's conversation.
    /// </summary>
    public required string CurrentNodeKey { get; set; }

    public DateTime UpdatedUtc { get; set; }
}

