namespace EndlessNight.Domain;

/// <summary>
/// Tunable knobs for how meta-memory gets applied.
/// </summary>
public sealed class MetaMemoryConfig
{
    /// <summary>
    /// Probability (0..1) that a subtle memory note manifests as flavor text.
    /// </summary>
    public float SubtleFlavorChance { get; init; } = 0.25f;

    /// <summary>
    /// Probability (0..1) that aggressive mode enforces a hard consequence.
    /// </summary>
    public float AggressiveHardGateChance { get; init; } = 0.6f;

    /// <summary>
    /// Maximum number of aggressive flags that can trigger per room/turn.
    /// </summary>
    public int AggressiveMaxTriggersPerRoom { get; init; } = 2;
}

