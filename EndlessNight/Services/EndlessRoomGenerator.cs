using EndlessNight.Domain;

namespace EndlessNight.Services;

/// <summary>
/// Deterministic, non-LLM endless room generator.
/// Generates a small number of new rooms and connects them to the existing graph.
/// </summary>
public sealed class EndlessRoomGenerator
{
    public sealed record GeneratedRoom(RoomInstance Room, IReadOnlyList<WorldObjectInstance> Objects);

    public GeneratedRoom GenerateRoom(Guid runId, int seed, int cursor, int x, int y, int parentDepth,
        int baseDanger, float dangerScale, IReadOnlyList<string> enabledLorePacks)
    {
        // Cursor is included so generation is stable per run even when called at different times.
        var rng = new Random(HashCode.Combine(seed, cursor, x, y));

        var depth = Math.Max(0, parentDepth + 1);

        // Scale danger with depth and difficulty multiplier.
        var scaledBase = baseDanger + (int)MathF.Floor(depth * 0.15f * dangerScale);
        var (name, desc, danger) = GenerateRoomCore(rng, scaledBase);

        var tags = GenerateTags(rng, enabledLorePacks, danger, depth);

        var room = new RoomInstance
        {
            RunId = runId,
            RoomId = Guid.NewGuid(),
            Name = name,
            Description = desc,
            DangerRating = danger,
            Loot = new List<string>(),
            X = x,
            Y = y,
            Depth = depth,
            RoomTags = tags
        };

        // Deterministic object/loot seeding, scaled by difficulty and depth.
        // We don't have the loot multiplier in this signature yet; encode simple scaling via a tag.
        // FrontierExpansionService passes difficulty scaling via dangerScale today; we'll pass lootScale next.
        var baseItemChance = 0.18;
        var depthLootPenalty = Math.Clamp(depth * 0.004, 0.0, 0.10); // deeper = slightly fewer freebies
        var itemChance = Math.Clamp(baseItemChance - depthLootPenalty, 0.05, 0.30);

        var objects = new List<WorldObjectInstance>();
        if (rng.NextDouble() < itemChance)
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

    private static List<string> GenerateTags(Random rng, IReadOnlyList<string> enabledLorePacks, int danger, int depth)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "procedural",
            "indoors"
        };

        // Baseline cosmic vibe.
        if (enabledLorePacks.Any(p => p.Equals("cosmic-horror", StringComparison.OrdinalIgnoreCase)))
            tags.Add("cosmic");

        // Danger-driven seasoning.
        if (danger >= 3) tags.Add("hostile");
        if (danger >= 4) tags.Add("terror");
        if (depth >= 5) tags.Add("deep");

        // Lore packs influence flavor tags.
        // Increase odds as danger/depth climb so references become more prominent under pressure.
        var pressure = Math.Clamp((danger * 0.08) + (depth * 0.02), 0.0, 0.65);

        if (enabledLorePacks.Any(p => p.Equals("lovecraft", StringComparison.OrdinalIgnoreCase)))
            if (rng.NextDouble() < 0.25 + pressure) tags.Add("eldritch");

        if (enabledLorePacks.Any(p => p.Equals("zork", StringComparison.OrdinalIgnoreCase)))
            if (rng.NextDouble() < 0.15 + pressure * 0.6) tags.Add("zorkish");

        if (enabledLorePacks.Any(p => p.Equals("undertale", StringComparison.OrdinalIgnoreCase)))
            if (rng.NextDouble() < 0.10 + Math.Clamp(0.25 - pressure, 0.0, 0.25)) tags.Add("undertaleish");

        // Environmental tags.
        var env = new[] { "dust", "echoes", "stone", "cold", "whispers", "damp", "moths" };
        tags.Add(env[rng.Next(env.Length)]);

        return tags.ToList();
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

    // Backwards-compatible overload (used by older callers). Keeps behavior stable.
    public GeneratedRoom GenerateRoom(Guid runId, int seed, int cursor, int x, int y, int dangerBase)
        => GenerateRoom(runId, seed, cursor, x, y, parentDepth: 0, baseDanger: dangerBase, dangerScale: 1.0f, enabledLorePacks: Array.Empty<string>());
}
