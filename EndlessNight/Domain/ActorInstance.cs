using EndlessNight.Domain.Abstractions;
using EndlessNight.Domain.Actors;

namespace EndlessNight.Domain;

public enum ActorKind
{
    Npc,
    Enemy
}

public enum ActorDisposition
{
    Unknown,
    Friendly,
    Hostile
}

/// <summary>
/// An NPC or enemy spawned for a specific run and placed in a room.
/// </summary>
public sealed class ActorInstance : IEnemy, INpc
{
    public Guid Id { get; set; }

    public required Guid RunId { get; set; }

    public required ActorKind Kind { get; set; }

    public required string Name { get; set; } = string.Empty;

    public required Guid CurrentRoomId { get; set; }

    /// <summary>
    /// 0..100. For enemies could be aggression; for NPC could be fear.
    /// </summary>
    public int Intensity { get; set; }

    /// <summary>
    /// -100..100 (Evil..Good). 0 is neutral.
    /// </summary>
    public int Morality { get; set; }

    /// <summary>
    /// 0..100. Only meaningful for NPCs.
    /// </summary>
    public int Sanity { get; set; } = 100;

    /// <summary>
    /// How the player currently perceives this actor (Unknown/Friendly/Hostile).
    /// </summary>
    public ActorDisposition Disposition { get; set; } = ActorDisposition.Unknown;

    /// <summary>
    /// Only meaningful for enemies. If true, the actor will behave as hostile.
    /// </summary>
    public bool IsHostile { get; set; }

    /// <summary>
    /// Only meaningful for enemies. If true, the enemy is pacified for this run.
    /// </summary>
    public bool IsPacified { get; set; }

    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// Stable ordering index for room-entry auto dialogue. Lower speaks first.
    /// </summary>
    public int SpawnIndex { get; set; }

    /// <summary>
    /// If true, this actor may speak automatically when the player enters their room.
    /// Primarily used for NPCs.
    /// </summary>
    public bool AutoSpeakOnEnter { get; set; }

    /// <summary>
    /// 0 = simple (short phrases), 1 = normal, 2 = advanced (full conversation).
    /// For enemies, simple is typical unless they're "advanced".
    /// </summary>
    public int SpeechLevel { get; set; }

    /// <summary>
    /// Enemy-only: tracks progress toward unlocking pacify through conversation.
    /// </summary>
    public int PacifyProgress { get; set; }

    /// <summary>
    /// Enemy-only: whether pacify has been unlocked via dialogue.
    /// </summary>
    public bool PacifyUnlocked { get; set; }
}
