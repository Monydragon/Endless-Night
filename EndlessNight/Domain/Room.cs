using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class Room : IKeyedEntity, INamedEntity
{
    public Guid Id { get; set; }

    /// <summary>Stable key for content (used for idempotent seeding).</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    /// <summary>
    /// Direction -> target room key (e.g. "east" -> "library").
    /// Note: not currently used by the main procedural run loop.
    /// </summary>
    public Dictionary<string, string> Exits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
