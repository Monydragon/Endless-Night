namespace EndlessNight.Domain.Abstractions;

/// <summary>
/// Marker for entities whose data is scoped to a specific run/playthrough.
/// </summary>
public interface IRunScoped : IEntity
{
    Guid RunId { get; set; }
}

