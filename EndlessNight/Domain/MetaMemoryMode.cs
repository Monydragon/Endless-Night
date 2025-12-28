namespace EndlessNight.Domain;

/// <summary>
/// Controls how strongly past runs influence the current one.
/// Stored as a setting so players can tune persistence versus determinism.
/// </summary>
public enum MetaMemoryMode
{
    None = 0,
    Subtle = 1,
    Aggressive = 2
}

