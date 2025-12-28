using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Cross-run memory flags. Use sparingly; promote only whitelisted outcomes.
/// Stored in SQLite and can influence future runs deterministically via a seed offset.
/// </summary>
public sealed class WorldFlag : IEntity
{
    public Guid Id { get; set; }

    public required string Key { get; set; }

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

