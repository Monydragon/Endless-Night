using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Global (cross-run) settings stored in SQLite.
/// We store values as strings for flexibility and forward compatibility.
/// </summary>
public sealed class GameSetting : IEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable key (e.g. "debug.enabled", "ui.sanitySlip.enabled").
    /// </summary>
    public required string Key { get; set; }

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

