using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Minimal event log for a run. Useful for debugging and later replay/quest logic.
/// </summary>
public sealed class RoomEventLog : IRunScoped
{
    public Guid Id { get; set; }

    public required Guid RunId { get; set; }

    public int Turn { get; set; }

    public required string EventType { get; set; }

    public required string Message { get; set; }

    public DateTime CreatedUtc { get; set; }
}
