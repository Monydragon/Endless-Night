namespace EndlessNight.Domain.Abstractions;

/// <summary>
/// Common contract for content entities that have a stable string key.
/// </summary>
public interface IKeyedEntity : IEntity
{
    string Key { get; set; }
}

