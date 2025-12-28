using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public enum FlagScopeType
{
    Global = 0,
    Actor = 1,
    Room = 2,
}

/// <summary>
/// Per-run memory flags. These are deterministic inputs into dialogue/encounters.
/// </summary>
public sealed class RunFlag : IEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public FlagScopeType ScopeType { get; set; } = FlagScopeType.Global;

    /// <summary>
    /// Optional scope id (e.g. actorId or roomId) depending on ScopeType.
    /// Guid.Empty for Global.
    /// </summary>
    public Guid ScopeId { get; set; } = Guid.Empty;

    public required string Key { get; set; }

    public string Value { get; set; } = "";

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

