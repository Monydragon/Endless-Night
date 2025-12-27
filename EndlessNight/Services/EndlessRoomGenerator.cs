using EndlessNight.Domain;

namespace EndlessNight.Services;

/// <summary>
/// Deterministic, non-LLM endless room generator.
/// Generates a small number of new rooms and connects them to the existing graph.
/// </summary>
public sealed class EndlessRoomGenerator
{
    public sealed record GeneratedRoom(RoomInstance Room, IReadOnlyList<WorldObjectInstance> Objects);

    public GeneratedRoom GenerateRoom(Guid runId, int seed, int cursor, int x, int y, int dangerBase)
    {
        // Cursor is included so generation is stable per run even when called at different times.
        var rng = new Random(HashCode.Combine(seed, cursor, x, y));

        var (name, desc, danger) = GenerateRoomCore(rng, dangerBase);

        var room = new RoomInstance
        {
            RunId = runId,
            RoomId = Guid.NewGuid(),
            Name = name,
            Description = desc,
            DangerRating = danger,
            Loot = new List<string>(),
            X = x,
            Y = y
        };

        // Very small amount of deterministic object seeding (expanded later)
        var objects = new List<WorldObjectInstance>();
        if (rng.NextDouble() < 0.18)
        {
            objects.Add(new WorldObjectInstance
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                RoomId = room.RoomId,
                Kind = WorldObjectKind.GroundItem,
                Key = $"ground.note.{cursor}",
                Name = "note",
                Description = "A note scratched with trembling intent.",
                IsHidden = rng.NextDouble() < 0.3,
                ItemKey = "note",
                Quantity = 1
            });
        }

        return new GeneratedRoom(room, objects);
    }

    private static (string name, string desc, int danger) GenerateRoomCore(Random rng, int dangerBase)
    {
        // Keep the style consistent with the existing generator.
        var nameBitsA = new[] { "Hall", "Passage", "Gallery", "Stair", "Landing", "Study", "Parlor", "Cellar" };
        var nameBitsB = new[] { "Echoes", "Ash", "Whispers", "Forgotten Songs", "Quiet Teeth", "Old Runes", "Cold Light" };

        var descBits = new[]
        {
            "The air tastes like pennies and old paper.",
            "Something in the corners refuses to be seen directly.",
            "Your footsteps arrive a heartbeat late.",
            "The walls hold their shape with stubborn effort.",
            "A draft moves like a finger tracing your spine.",
            "The darkness is patient and well-fed.",
        };

        var name = rng.NextDouble() < 0.55
            ? $"{nameBitsA[rng.Next(nameBitsA.Length)]} of {nameBitsB[rng.Next(nameBitsB.Length)]}"
            : nameBitsA[rng.Next(nameBitsA.Length)];

        var desc = descBits[rng.Next(descBits.Length)];

        var danger = Math.Clamp(dangerBase + rng.Next(-1, 2), 0, 5);
        return (name, desc, danger);
    }
}

