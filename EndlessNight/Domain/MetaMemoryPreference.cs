namespace EndlessNight.Domain;

/// <summary>
/// Player-configurable knobs for how meta-memory affects new runs.
/// For now it wraps an enum, but we can expand it with sliders/weights later.
/// </summary>
public sealed class MetaMemoryPreference
{
    public MetaMemoryMode Mode { get; init; } = MetaMemoryMode.Subtle;

    public static MetaMemoryPreference FromString(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "none" => new MetaMemoryPreference { Mode = MetaMemoryMode.None },
            "aggressive" => new MetaMemoryPreference { Mode = MetaMemoryMode.Aggressive },
            _ => new MetaMemoryPreference { Mode = MetaMemoryMode.Subtle }
        };

    public string ToStorageString() => Mode.ToString().ToLowerInvariant();
}

