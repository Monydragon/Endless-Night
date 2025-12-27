using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class ItemDefinition : IKeyedEntity, INamedEntity
{
    public Guid Id { get; set; }

    public required string Key { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public List<string> Tags { get; set; } = new();
}
