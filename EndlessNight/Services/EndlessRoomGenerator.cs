using EndlessNight.Domain;

namespace EndlessNight.Services;

/// <summary>
/// Deterministic, non-LLM endless room generator.
/// Generates a small number of new rooms and connects them to the existing graph.
/// </summary>
public sealed class EndlessRoomGenerator
{
    public sealed record GeneratedRoom(RoomInstance Room, IReadOnlyList<WorldObjectInstance> Objects);

    private sealed record LootEntry(
        string ItemKey,
        string Name,
        string Description,
        double Weight,
        double MinPressure,
        string? RequiresLorePack);

    public GeneratedRoom GenerateRoom(Guid runId, int seed, int cursor, int x, int y, int parentDepth,
        int baseDanger, float dangerScale, float lootScale, IReadOnlyList<string> enabledLorePacks)
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

        var pressure = Math.Clamp((danger * 0.08) + (depth * 0.02), 0.0, 0.65);

        // Deterministic loot seeding: quantity and type are scaled by lootScale and depth.
        var objects = new List<WorldObjectInstance>();

        var baseAnyLootChance = 0.22f;
        var scaledChance = baseAnyLootChance * Math.Clamp(lootScale, 0.5f, 2.0f);
        var depthPenalty = Math.Clamp(depth * 0.004f, 0.0f, 0.12f);
        var anyLootChance = Math.Clamp(scaledChance - depthPenalty, 0.04f, 0.55f);

        // Chance to spawn a chest instead of a plain ground item.
        var chestChance = Math.Clamp(0.08 + pressure * 0.10, 0.08, 0.22);

        // Chance to place a hidden "search cache" that becomes visible on search.
        var cacheChance = Math.Clamp(0.06 + pressure * 0.08, 0.06, 0.18);

        // Up to 2 loot rolls on generous lootScale.
        var extraRollChance = Math.Clamp((lootScale - 1.0f) * 0.25f, 0.0f, 0.25f);
        var rolls = 1 + (rng.NextDouble() < extraRollChance ? 1 : 0);

        // Build loot table for this room.
        var table = BuildLootTable();

        // Deterministic guarantee: once pressure is non-trivial, ensure at least one lore-pack item is possible.
        // This keeps tests stable without making every room a reference fest.
        var guaranteeLoreItem = pressure >= 0.12 && enabledLorePacks.Count > 0;
        var loreGuaranteed = false;

        for (var r = 0; r < rolls; r++)
        {
            if (rng.NextDouble() >= anyLootChance)
                continue;

            var entry = RollLoot(rng, table, pressure, enabledLorePacks, guaranteeLoreItem && !loreGuaranteed);
            if (entry is null)
                continue;

            if (entry.RequiresLorePack is not null)
                loreGuaranteed = true;

            // Prefer to use different object keys for multiple rolls.
            var suffix = rolls == 1 ? "" : $".{r + 1}";

            // Decide delivery method.
            var roll = rng.NextDouble();
            if (roll < chestChance)
            {
                // Chest contains 1-3 items; use the rolled entry plus extra pulls.
                var lootKeys = new List<string> { entry.ItemKey };
                var bonusCount = rng.Next(0, 2); // +0..1 extra
                for (var i = 0; i < bonusCount; i++)
                {
                    var extra = RollLoot(rng, table, pressure, enabledLorePacks, forceLore: false);
                    if (extra is not null) lootKeys.Add(extra.ItemKey);
                }

                objects.Add(new WorldObjectInstance
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    RoomId = room.RoomId,
                    Kind = WorldObjectKind.Chest,
                    Key = $"proc.chest.{cursor}{suffix}",
                    Name = "Chest",
                    Description = "A warped chest. The latch looks weak.",
                    IsHidden = rng.NextDouble() < 0.20,
                    RequiredItemKey = null,
                    LootItemKeys = lootKeys
                });
            }
            else if (roll < chestChance + cacheChance)
            {
                // Search cache: a hidden ground item that gets revealed on search.
                objects.Add(new WorldObjectInstance
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    RoomId = room.RoomId,
                    Kind = WorldObjectKind.GroundItem,
                    Key = $"proc.cache.{cursor}{suffix}",
                    Name = entry.Name,
                    Description = entry.Description,
                    IsHidden = true,
                    ItemKey = entry.ItemKey,
                    Quantity = 1
                });
            }
            else
            {
                // Plain ground item.
                objects.Add(new WorldObjectInstance
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    RoomId = room.RoomId,
                    Kind = WorldObjectKind.GroundItem,
                    Key = $"proc.item.{cursor}{suffix}",
                    Name = entry.Name,
                    Description = entry.Description,
                    IsHidden = rng.NextDouble() < 0.30,
                    ItemKey = entry.ItemKey,
                    Quantity = 1
                });
            }
        }

        return new GeneratedRoom(room, objects);
    }

    private static List<LootEntry> BuildLootTable()
    {
        // First-pass item pool. Keep keys compatible with seeded ItemDefinitions where possible.
        // 'Weight' is relative; 'MinPressure' gates rare items to deeper/dangerous rooms.
        return new List<LootEntry>
        {
            new("note", "note", "A note scratched with trembling intent.", 10, 0.0, null),
            new("bandage", "bandage", "Fresh cloth wrapped tight. Someone prepared for failure.", 8, 0.0, null),
            new("torch", "torch", "A wooden torch. Not much comfort, but it pushes back the dark.", 6, 0.0, null),
            new("lantern", "lantern", "A lantern with a stubborn wick.", 4, 0.05, null),
            new("rope", "rope", "Thick hemp rope, frayed but strong.", 3, 0.08, null),

            // Lore-pack themed items (homage-style, no direct quotes).
            new("eldritch-idol", "idol", "A small idol. It seems to watch from angles you can't name.", 2, 0.18, "lovecraft"),
            new("brass-lantern", "brass lantern", "A brass lantern with a too-familiar weight.", 2, 0.15, "zork"),
            new("blue-heart-charm", "charm", "A small charm shaped like a blue heart. It thrums faintly.", 2, 0.12, "undertale"),
        };
    }

    private static LootEntry? RollLoot(
        Random rng,
        List<LootEntry> table,
        double pressure,
        IReadOnlyList<string> enabledLorePacks,
        bool forceLore)
    {
        // Filter by pressure and enabled lore packs.
        var eligible = table
            .Where(e => pressure >= e.MinPressure)
            .Where(e => e.RequiresLorePack is null || enabledLorePacks.Any(p => p.Equals(e.RequiresLorePack, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (eligible.Count == 0)
            return null;

        if (forceLore)
        {
            var lore = eligible.Where(e => e.RequiresLorePack is not null).ToList();
            if (lore.Count > 0)
                eligible = lore;
        }

        var total = eligible.Sum(e => e.Weight);
        var roll = rng.NextDouble() * total;
        foreach (var e in eligible)
        {
            roll -= e.Weight;
            if (roll <= 0)
                return e;
        }

        return eligible[^1];
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
}
