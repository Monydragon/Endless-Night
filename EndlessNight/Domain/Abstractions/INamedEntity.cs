namespace EndlessNight.Domain.Abstractions;

/// <summary>
/// Common contract for entities that have a display name.
/// </summary>
public interface INamedEntity : IEntity
{
    string Name { get; set; }
}

