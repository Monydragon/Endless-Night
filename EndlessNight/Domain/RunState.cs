using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class RunState : IEntity
{
    public Guid Id { get; set; }

    /// <summary>Stable save slot owner.</summary>
    public required string PlayerName { get; set; }

    /// <summary>Unique run identifier (multiple runs per player supported).</summary>
    public required Guid RunId { get; set; }

    public required int Seed { get; set; }

    public int Turn { get; set; }

    /// <summary>
    /// 0..100 (higher is better).
    /// </summary>
    public int Sanity { get; set; } = 100;

    /// <summary>
    /// 0..100 (higher is better).
    /// </summary>
    public int Health { get; set; } = 100;

    /// <summary>
    /// -100..100 (Evil..Good). 0 is neutral.
    /// </summary>
    public int Morality { get; set; } = 0;

    /// <summary>Which room instance the player is currently in.</summary>
    public required Guid CurrentRoomId { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
