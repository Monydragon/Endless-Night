namespace EndlessNight.Domain.Actors;

public interface IEnemy : IActor
{
    /// <summary>
    /// If true, the enemy may start in a deceptive/unknown state.
    /// </summary>
    bool IsHostile { get; set; }

    /// <summary>
    /// If true, the enemy has been pacified for this run.
    /// </summary>
    bool IsPacified { get; set; }
}

