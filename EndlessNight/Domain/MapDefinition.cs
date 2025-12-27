using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class MapDefinition : IKeyedEntity, INamedEntity
{
    public Guid Id { get; set; }

    /// <summary>Stable key for content (used for idempotent seeding).</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    public required string StartingRoomKey { get; set; }
}
