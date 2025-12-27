namespace EndlessNight.Domain.Abstractions;

/// <summary>
/// Base identity contract for persisted entities.
/// </summary>
public interface IEntity
{
    Guid Id { get; set; }
}

