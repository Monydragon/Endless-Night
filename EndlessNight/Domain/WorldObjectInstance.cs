using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class WorldObjectInstance : IRunScoped
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid RoomId { get; set; }

    public WorldObjectKind Kind { get; set; }

    /// <summary>
    /// Stable per-room key used for validation/debugging (not required to be globally unique).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    // Common state flags
    public bool IsConsumed { get; set; }
    public bool IsOpened { get; set; }
    public bool IsDisarmed { get; set; }
    public bool IsTriggered { get; set; }
    public bool IsSolved { get; set; }

    // Item / loot
    public string? ItemKey { get; set; }
    public int Quantity { get; set; }
    public List<string> LootItemKeys { get; set; } = new();

    // Requirements
    public string? RequiredItemKey { get; set; }

    // Trap payload
    public TrapTrigger? TrapTrigger { get; set; }
    public int HealthDelta { get; set; }
    public int SanityDelta { get; set; }

    // Puzzle gate payload
    public Direction? BlocksDirection { get; set; }
}

