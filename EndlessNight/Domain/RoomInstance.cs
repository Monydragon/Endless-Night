using System;
using System.Collections.Generic;
using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

public sealed class RoomInstance : IRunScoped, INamedEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public required Guid RoomId { get; set; }

    public string Name { get; set; } = string.Empty;

    public required string Description { get; set; }

    public Dictionary<Direction, Guid> Exits { get; set; } = new();

    public bool HasBeenSearched { get; set; }

    public List<string> Loot { get; set; } = new();

    public int DangerRating { get; set; }

    public bool TrapTriggered { get; set; }

    /// <summary>
    /// Once true, this room will not spawn new enemies anymore.
    /// (Existing enemies may still move/follow into it.)
    /// </summary>
    public bool IsCleared { get; set; }

    // Added grid coordinates for display/debugging
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>
    /// Generation depth from the starting room (parent depth + 1).
    /// Cheap, stable, and good for difficulty pacing.
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Procedural tags for this room used by dialogue/description systems.
    /// Stored as JSON in SQLite.
    /// </summary>
    public List<string> RoomTags { get; set; } = new();
}
