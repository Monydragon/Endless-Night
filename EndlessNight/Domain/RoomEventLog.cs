using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Minimal event log for a run. Useful for debugging and later replay/quest logic.
/// </summary>
public sealed class RoomEventLog : IRunScoped
{
    public Guid Id { get; set; }

    public required Guid RunId { get; set; }

    /// <summary>
    /// Optional actor associated with this event (e.g. procedural dialogue, encounters).
    /// </summary>
    public Guid? ActorId { get; set; }

    public int Turn { get; set; }

    public required string EventType { get; set; }

    public required string Message { get; set; }

    public DateTime CreatedUtc { get; set; }
}
