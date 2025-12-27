using EndlessNight.Domain;

namespace EndlessNight.Services;

/// <summary>
/// Generates a small, unique "level 1" map (a room graph) per run and persists it as RoomInstance documents.
///</summary>
public sealed class ProceduralLevel1Generator
{
    public IReadOnlyList<RoomInstance> Generate(Guid runId, int seed)
        => GenerateWorld(runId, seed).Rooms;

    public WorldGenerationResult GenerateWorld(Guid runId, int seed)
    {
        var rng = new Random(seed);

        // Simple first pass: generate 8 rooms in a line with a few branches.
        // This is intentionally small but provides a foundation for expansion.
        var roomCount = rng.Next(7, 10);
        var rooms = new List<RoomInstance>(roomCount);

        // Track a simple coordinate grid: start at (0,0) and step north increases Y.
        var x = 0;
        var y = 0;

        for (var i = 0; i < roomCount; i++)
        {
            var roomId = Guid.NewGuid();

            var (name, desc, danger) = i switch
            {
                0 => ("Foyer", "A narrow foyer. The air is cold and tastes of dust.", 0),
                1 => ("Hallway", "The corridor stretches too far for the size of the house.", 1),
                2 => ("Library", "Shelves lean like tired men. Something scratches behind the books.", 2),
                _ => GenerateGenericRoom(rng)
            };

            // Dynamic suffix to make rooms feel fresh
            var dynamicSuffix = rng.NextDouble() < 0.4 ? GetAtmosphericSuffix(rng) : string.Empty;
            var dynamicName = dynamicSuffix.Length > 0 ? $"{name} of {dynamicSuffix}" : name;

            rooms.Add(new RoomInstance
            {
                RunId = runId,
                RoomId = roomId,
                Name = dynamicName,
                Description = desc,
                DangerRating = danger,
                Loot = new List<string>(),
                X = x,
                Y = y
            });

            // Advance coordinates: main chain goes North
            y += 1;
        }

        // Connect rooms in a chain: North/South.
        for (var i = 0; i < rooms.Count - 1; i++)
        {
            rooms[i].Exits[Direction.North] = rooms[i + 1].RoomId;
            rooms[i + 1].Exits[Direction.South] = rooms[i].RoomId;
        }

        // Add a random branch or two.
        var branches = rng.Next(1, 3);
        for (var b = 0; b < branches; b++)
        {
            var fromIndex = rng.Next(1, rooms.Count - 2);
            var toIndex = rng.Next(fromIndex + 1, rooms.Count);
            var dir = rng.Next(0, 2) == 0 ? Direction.East : Direction.West;
            var opposite = dir == Direction.East ? Direction.West : Direction.East;

            // Assign coordinates crudely for branch endpoints
            var from = rooms[fromIndex];
            var to = rooms[toIndex];
            to.X = from.X + (dir == Direction.East ? 1 : -1);
            to.Y = from.Y + rng.Next(0, 2); // slight vertical variance

            // Don't overwrite an existing exit
            if (!rooms[fromIndex].Exits.ContainsKey(dir) && !rooms[toIndex].Exits.ContainsKey(opposite))
            {
                rooms[fromIndex].Exits[dir] = rooms[toIndex].RoomId;
                rooms[toIndex].Exits[opposite] = rooms[fromIndex].RoomId;
            }
        }

        var objects = new List<WorldObjectInstance>();

        // Ground items: explicit items that can be hidden and revealed by searching.
        var groundItemCandidates = new[] { "bandage", "note", "health-potion", "torch", "rope" };
        foreach (var item in groundItemCandidates.OrderBy(_ => rng.Next()).Take(rng.Next(2, 4)))
        {
            var target = rooms[rng.Next(0, rooms.Count)];
            objects.Add(new WorldObjectInstance
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                RoomId = target.RoomId,
                Kind = WorldObjectKind.GroundItem,
                Key = $"ground.{item}",
                Name = item,
                Description = $"A {item.Replace('-', ' ')} lies here.",
                IsHidden = rng.NextDouble() < 0.40,
                ItemKey = item,
                Quantity = 1
            });
        }

        // Always place at least one key item early enough to support item-gated puzzles.
        // We keep keys as normal inventory items for now.
        var keyItemPool = new[] { "rusty-key", "sigil", "silver-key", "crystal-shard", "ancient-tome" };
        var pickedKeyItem = keyItemPool[rng.Next(0, keyItemPool.Length)];
        var keyRoomIndex = rng.Next(0, Math.Max(1, rooms.Count / 2));
        objects.Add(new WorldObjectInstance
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            RoomId = rooms[keyRoomIndex].RoomId,
            Kind = WorldObjectKind.GroundItem,
            Key = $"key.{pickedKeyItem}",
            Name = pickedKeyItem,
            Description = pickedKeyItem switch
            {
                "rusty-key" => "A rusted key, cold as regret.",
                "sigil" => "A small artifact etched with a chalky sigil.",
                "silver-key" => "An ornate silver key, cold to the touch.",
                "crystal-shard" => "A shard of obsidian crystal that reflects light that isn't there.",
                "ancient-tome" => "A leather-bound book. The pages whisper secrets.",
                _ => $"A mysterious {pickedKeyItem.Replace('-', ' ')}."
            },
            IsHidden = false,
            ItemKey = pickedKeyItem,
            Quantity = 1
        });

        // Chests: contain 1-3 items. Some require keys to open.
        var chestCount = rng.Next(2, 4);
        for (var c = 0; c < chestCount; c++)
        {
            var target = rooms[rng.Next(1, rooms.Count)];
            var requiresKey = rng.NextDouble() < 0.40;
            var requiredKey = requiresKey ? (rng.NextDouble() < 0.7 ? "rusty-key" : "silver-key") : null;

            var loot = new List<string>();
            var lootCandidates = new[] { "bandage", "health-potion", "note", "lantern", "torch", "rope", "ancient-tome" };
            foreach (var li in lootCandidates.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4)))
                loot.Add(li);

            objects.Add(new WorldObjectInstance
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                RoomId = target.RoomId,
                Kind = WorldObjectKind.Chest,
                Key = $"chest.{c + 1}",
                Name = requiresKey ? "Locked Chest" : "Chest",
                Description = requiresKey 
                    ? $"A warped chest with a {(requiredKey == "silver-key" ? "silver" : "corroded")} lock." 
                    : "A warped chest. The latch looks weak.",
                IsHidden = rng.NextDouble() < 0.20,
                RequiredItemKey = requiredKey,
                LootItemKeys = loot
            });
        }

        // Traps: varied room-level hazards with different triggers and effects.
        var trapTypes = new[]
        {
            (key: "trap.tripwire", name: "Tripwire", desc: "A thin wire sits at ankle height. Easy to miss.", trigger: TrapTrigger.OnSearchRoom),
            (key: "trap.pressure-plate", name: "Pressure Plate", desc: "The floor tile here looks wrong.", trigger: TrapTrigger.OnEnterRoom),
            (key: "trap.dart", name: "Dart Trap", desc: "Tiny holes in the wall. Something waits inside.", trigger: TrapTrigger.OnInteract),
            (key: "trap.poison-gas", name: "Gas Vent", desc: "A faint hissing sound comes from the corner.", trigger: TrapTrigger.OnEnterRoom)
        };

        foreach (var room in rooms.Where(r => r.DangerRating >= 2).OrderBy(_ => rng.Next()).Take(rng.Next(1, 3)))
        {
            var trapType = trapTypes[rng.Next(0, trapTypes.Length)];
            objects.Add(new WorldObjectInstance
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                RoomId = room.RoomId,
                Kind = WorldObjectKind.Trap,
                Key = trapType.key,
                Name = trapType.name,
                Description = trapType.desc,
                IsHidden = rng.NextDouble() < 0.65,
                TrapTrigger = trapType.trigger,
                HealthDelta = -rng.Next(3, 10),
                SanityDelta = -rng.Next(0, 4)
            });
        }

        // Campfires: 2-3 per level; used to restore sanity and health.
        var campCount = rng.Next(2, 4);
        for (var cf = 0; cf < campCount; cf++)
        {
            var target = rooms[rng.Next(0, rooms.Count)];
            objects.Add(new WorldObjectInstance
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                RoomId = target.RoomId,
                Kind = WorldObjectKind.Campfire,
                Key = $"campfire.{cf + 1}",
                Name = "Firepit",
                Description = "A small firepit crackles here. Warmth feels almost like a memory.",
                IsHidden = false
            });
        }

        // Puzzle gate: blocks one exit direction and requires a key item.
        // IMPORTANT: Only keep the puzzle if we can prove solvable with placed items/loot.
        if (rooms.Count >= 4)
        {
            var gateRoom = rooms[rng.Next(1, rooms.Count - 1)];
            var candidateDirs = gateRoom.Exits.Keys.ToList();
            if (candidateDirs.Count > 0)
            {
                var blocks = candidateDirs[rng.Next(0, candidateDirs.Count)];

                // Choose the gate that matches our picked key item
                var gateDefinition = PuzzleDefinition.KeyItemGates.FirstOrDefault(g => g.requiredItemKey == pickedKeyItem);
                if (gateDefinition != default)
                {
                    objects.Add(new WorldObjectInstance
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        RoomId = gateRoom.RoomId,
                        Kind = WorldObjectKind.PuzzleGate,
                        Key = gateDefinition.key,
                        Name = gateDefinition.name,
                        Description = gateDefinition.description,
                        IsHidden = false,
                        RequiredItemKey = gateDefinition.requiredItemKey,
                        BlocksDirection = blocks
                    });

                    // Validate solvability. If not solvable, strip all puzzle gates.
                    var startRoomId = rooms[0].RoomId;
                    if (!PuzzleSolvabilityValidator.AreAllGatesSolvable(rooms, objects, startRoomId))
                    {
                        objects.RemoveAll(o => o.Kind == WorldObjectKind.PuzzleGate);
                    }
                }
            }
        }

        // Keep a tiny legacy loot drip so SearchRoom still does something even without interactions.
        var legacyLootCandidates = new[] { "lantern", "bandage" };
        foreach (var item in legacyLootCandidates.OrderBy(_ => rng.Next()).Take(1))
        {
            var target = rooms[rng.Next(0, rooms.Count)];
            target.Loot.Add(item);
        }

        return new WorldGenerationResult(rooms, objects);
    }

    private static (string name, string description, int danger) GenerateGenericRoom(Random rng)
    {
        var variants = new (string name, string desc, int danger)[]
        {
            ("Dining Room", "A long table is set for guests who never arrived.", 1),
            ("Kitchen", "Pots hang still. The smell underneath them is wrong.", 2),
            ("Parlor", "A piano key depresses itself, slowly.", 2),
            ("Nursery", "A cradle rocks. No wind. No child.", 3),
            ("Bathroom", "The mirror fogs from the inside.", 2),
            ("Storage", "Old sheets cover shapes that don't match furniture.", 1)
        };

        return variants[rng.Next(0, variants.Length)];
    }

    private static string GetAtmosphericSuffix(Random rng)
    {
        var suffixes = new[]
        {
            "Ashes", "Murmurs", "Echoes", "Regret", "Shadows", "Old Names", "Forgotten Songs"
        };
        return suffixes[rng.Next(0, suffixes.Length)];
    }
}
