namespace EndlessNight.Domain;

public sealed class SaveGame
{
    public Guid Id { get; set; }

    public required string PlayerName { get; set; }

    public required string MapKey { get; set; }

    public required string CurrentRoomKey { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
