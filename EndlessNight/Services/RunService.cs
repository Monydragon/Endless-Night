using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
using EndlessNight.Domain.Story;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed class RunService
{
    private readonly SqliteDbContext _db;
    private readonly ProceduralLevel1Generator _generator;

    // Debug flag: can be toggled by Program via property
    public bool DebugMode { get; set; }

    public RunService(SqliteDbContext db, ProceduralLevel1Generator generator)
    {
        _db = db;
        _generator = generator;
    }

    public Task<RunState> CreateNewRunAsync(string playerName, CancellationToken cancellationToken = default)
        => CreateNewRunAsync(playerName, seed: null, difficultyKey: null, cancellationToken);

    public async Task<RunState> CreateNewRunAsync(string playerName, int? seed, CancellationToken cancellationToken = default)
        => await CreateNewRunAsync(playerName, seed, difficultyKey: null, cancellationToken);

    public async Task<RunState> CreateNewRunAsync(string playerName, int? seed, string? difficultyKey,
        CancellationToken cancellationToken = default)
    {
        playerName = NormalizePlayerName(playerName);

        var difficulty = await new DifficultyService(_db).GetOrDefaultAsync(difficultyKey, cancellationToken);

        // If caller provides an explicit seed, the run's topology should be reproducible.
        // Use a deterministic RunId in that case so any derived GUIDs remain stable across runs.
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        var runId = seed is null
            ? Guid.NewGuid()
            : DeterministicGuidFromSeed(resolvedSeed);

        // Generate world
        var world = _generator.GenerateWorld(runId, resolvedSeed);

        // If difficulty constrains room count, apply a deterministic cut of the generated chunk
        // for now (Milestone 2 will replace this with proper parameterized generation).
        if (!difficulty.IsEndless)
        {
            var maxRooms = Math.Clamp(difficulty.MaxRooms, 4, world.Rooms.Count);
            if (world.Rooms.Count > maxRooms)
            {
                var trimmedRooms = world.Rooms.Take(maxRooms).ToList();
                // Filter objects to only those in kept rooms.
                var keptRoomIds = trimmedRooms.Select(r => r.RoomId).ToHashSet();
                var trimmedObjects = world.Objects.Where(o => keptRoomIds.Contains(o.RoomId)).ToList();
                world = world with { Rooms = trimmedRooms, Objects = trimmedObjects };
            }
        }

        var rooms = world.Rooms
            .Select((r, index) =>
            {
                // Keep EF primary key deterministic per room to avoid nondeterminism leaks.
                // RoomId is the stable identity; Id is the EF row key.
                r.Id = DeterministicGuid(runId, "room-instance", index);

                // Ensure new fields have safe values for legacy generator rooms.
                // Seed-world rooms: use their Y coordinate (main chain) as a consistent depth proxy.
                r.Depth = Math.Max(0, r.Y);

                r.RoomTags ??= new List<string>();
                if (r.RoomTags.Count == 0)
                {
                    r.RoomTags.Add("seeded");
                    // Carry lore packs into seeded rooms too so UI/tests see consistent tags.
                    r.RoomTags.Add("cosmic");
                }

                return r;
            })
            .ToList();

        await _db.RoomInstances.AddRangeAsync(rooms, cancellationToken);

        // Persist world objects
        var objs = world.Objects
            .Select(o =>
            {
                // Ensure IDs are set even if generator forgot.
                if (o.Id == Guid.Empty)
                    o.Id = Guid.NewGuid();
                o.RunId = runId;
                return o;
            })
            .ToList();
        await _db.WorldObjects.AddRangeAsync(objs, cancellationToken);

        var startRoomId = rooms[0].RoomId; // Foyer

        var run = new RunState
        {
            Id = Guid.NewGuid(),
            PlayerName = playerName,
            RunId = runId,
            Seed = resolvedSeed,
            Turn = 0,
            Sanity = Math.Clamp(difficulty.StartingSanity, 0, 100),
            Health = Math.Clamp(difficulty.StartingHealth, 0, 100),
            Morality = 0,
            CurrentRoomId = startRoomId,
            DifficultyKey = difficulty.Key,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.Runs.Add(run);

        // Per-run config: future-proof for lore packs/endless toggles.
        _db.RunConfigs.Add(new RunConfig
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            DifficultyKey = difficulty.Key,
            EndlessEnabled = difficulty.IsEndless,
            MaxRooms = difficulty.IsEndless ? null : difficulty.MaxRooms,
            // Default packs can be tuned later via UI; keep cosmic-horror always on.
            EnabledLorePacks = new List<string> { "cosmic-horror", "lovecraft", "zork", "undertale" },
            DialogueSeedOffset = 17,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });

        // Seed a basic inventory item table entry (empty by default; add one placeholder light source sometimes).
        if (resolvedSeed % 2 == 0)
        {
            _db.RunInventoryItems.Add(new RunInventoryItem
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ItemKey = "lantern",
                Quantity = 1
            });
        }

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Turn = 0,
            EventType = "run.create",
            Message = "You wake up with the taste of dust and a name you almost remember.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Pre-generate a small frontier ring for endless runs.
        await new FrontierExpansionService(_db, new EndlessRoomGenerator())
            .EnsureFrontierRingAsync(run, cancellationToken);

        return run;
    }

    public async Task<List<RunState>> GetRunsAsync(string playerName, CancellationToken cancellationToken = default)
    {
        playerName = NormalizePlayerName(playerName);

        return await _db.Runs
            .Where(r => r.PlayerName == playerName)
            .OrderByDescending(r => r.UpdatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RunState?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await _db.Runs.FirstOrDefaultAsync(r => r.RunId == runId, cancellationToken);
    }

    public async Task<RoomInstance?> GetCurrentRoomAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.RoomInstances
            .FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == run.CurrentRoomId, cancellationToken);
    }

    public async Task<List<RunInventoryItem>> GetInventoryAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.RunInventoryItems
            .Where(i => i.RunId == run.RunId)
            .OrderBy(i => i.ItemKey)
            .ToListAsync(cancellationToken);
    }

    public async Task AddToInventoryAsync(RunState run, string itemKey, int quantity,
        CancellationToken cancellationToken = default)
    {
        itemKey = itemKey.Trim();
        if (itemKey.Length == 0 || quantity <= 0)
            return;

        var existing = await _db.RunInventoryItems
            .FirstOrDefaultAsync(i => i.RunId == run.RunId && i.ItemKey == itemKey, cancellationToken);

        if (existing is null)
        {
            _db.RunInventoryItems.Add(new RunInventoryItem
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ItemKey = itemKey,
                Quantity = quantity
            });
        }
        else
        {
            existing.Quantity += quantity;
            _db.RunInventoryItems.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryConsumeAsync(RunState run, string itemKey, int quantity,
        CancellationToken cancellationToken = default)
    {
        itemKey = itemKey.Trim();
        if (itemKey.Length == 0 || quantity <= 0)
            return false;

        var existing = await _db.RunInventoryItems
            .FirstOrDefaultAsync(i => i.RunId == run.RunId && i.ItemKey == itemKey, cancellationToken);

        if (existing is null || existing.Quantity < quantity)
            return false;

        existing.Quantity -= quantity;
        if (existing.Quantity <= 0)
            _db.RunInventoryItems.Remove(existing);
        else
            _db.RunInventoryItems.Update(existing);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveRunAsync(RunState run, CancellationToken cancellationToken = default)
    {
        ClampRunStats(run);
        run.UpdatedUtc = DateTime.UtcNow;
        _db.Runs.Update(run);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool ok, string? error)> MoveAsync(RunState run, Direction direction,
        CancellationToken cancellationToken = default)
    {
        var room = await GetCurrentRoomAsync(run, cancellationToken);
        if (room is null)
            return (false, "Your current room couldn't be loaded.");

        if (!room.Exits.TryGetValue(direction, out var nextRoomId))
            return (false, $"You can't go {direction} from here.");

        // Puzzle gate check: blocks a direction unless you have the required item.
        var gate = await _db.WorldObjects
            .Where(o => o.RunId == run.RunId && o.RoomId == room.RoomId && o.Kind == WorldObjectKind.PuzzleGate)
            .FirstOrDefaultAsync(o => o.BlocksDirection == direction && !o.IsSolved, cancellationToken);

        if (gate is not null)
        {
            if (string.IsNullOrWhiteSpace(gate.RequiredItemKey))
                return (false, "Something bars your way.");

            var has = await _db.RunInventoryItems
                .AnyAsync(i => i.RunId == run.RunId && i.ItemKey == gate.RequiredItemKey && i.Quantity > 0, cancellationToken);

            if (!has)
                return (false, $"{gate.Name}: You need '{gate.RequiredItemKey}'.");

            gate.IsSolved = true;
            _db.WorldObjects.Update(gate);

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "puzzle.solve",
                Message = $"You overcome: {gate.Name}.",
                CreatedUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        run.CurrentRoomId = nextRoomId;
        run.Turn++;

        // Ensure frontier ring after entering the new room (endless mode only).
        await new FrontierExpansionService(_db, new EndlessRoomGenerator())
            .EnsureFrontierRingAsync(run, cancellationToken);

        // Sanity drift based on danger (scaled by difficulty + depth)
        var nextRoom = await GetCurrentRoomAsync(run, cancellationToken);
        if (nextRoom is not null && run.Sanity > 0 && nextRoom.DangerRating >= 2)
        {
            var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);
            var depthFactor = 1.0f + (nextRoom.Depth * 0.03f);
            var scaled = diff.SanityDrainMultiplier * depthFactor;
            var drain = Math.Clamp((int)MathF.Ceiling(1.0f * scaled), 1, 5);
            run.Sanity = Math.Max(0, run.Sanity - drain);
        }

        // Creepy message chance inversely proportional to sanity
        var rng = new Random(run.Seed + run.Turn);
        var creepChance = Math.Clamp(1.0 - (run.Sanity / 100.0), 0.0, 0.85); // max 85%
        if (rng.NextDouble() < creepChance)
        {
            var msg = GetCreepyWhisper(rng);
            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "atmosphere.creep",
                Message = msg,
                CreatedUtc = DateTime.UtcNow
            });
        }

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "player.move",
            Message = $"You go {direction}.",
            CreatedUtc = DateTime.UtcNow
        });

        await SaveRunAsync(run, cancellationToken);
        return (true, null);
    }

    public async Task<List<WorldObjectInstance>> GetVisibleWorldObjectsInCurrentRoomAsync(RunState run,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.WorldObjects
            .Where(o => o.RunId == run.RunId && o.RoomId == run.CurrentRoomId)
            .Where(o => !o.IsHidden)
            .Where(o => !o.IsConsumed)
            .OrderBy(o => o.Kind)
            .ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);

        // In debug mode, bubble campfires to the top
        if (DebugMode)
            list = list.OrderByDescending(o => o.Kind == WorldObjectKind.Campfire).ThenBy(o => o.Name).ToList();

        return list;
    }

    public async Task<List<WorldObjectInstance>> GetHiddenWorldObjectsInCurrentRoomAsync(RunState run,
        CancellationToken cancellationToken = default)
    {
        return await _db.WorldObjects
            .Where(o => o.RunId == run.RunId && o.RoomId == run.CurrentRoomId)
            .Where(o => o.IsHidden)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool ok, string message)> InteractAsync(RunState run, Guid objectId,
        CancellationToken cancellationToken = default)
    {
        var obj = await _db.WorldObjects.FirstOrDefaultAsync(o => o.RunId == run.RunId && o.Id == objectId, cancellationToken);
        if (obj is null)
            return (false, "That isn't here.");

        if (obj.RoomId != run.CurrentRoomId)
            return (false, "You can't reach that from here.");

        if (obj.IsHidden)
            return (false, "You don't see that.");

        switch (obj.Kind)
        {
            case WorldObjectKind.GroundItem:
            {
                if (obj.IsConsumed)
                    return (false, "It's already gone.");

                if (string.IsNullOrWhiteSpace(obj.ItemKey) || obj.Quantity <= 0)
                    return (false, "You can't take that.");

                await AddToInventoryAsync(run, obj.ItemKey, obj.Quantity, cancellationToken);
                obj.IsConsumed = true;
                _db.WorldObjects.Update(obj);

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    EventType = "object.pickup",
                    Message = $"You pick up: {obj.ItemKey} x{obj.Quantity}.",
                    CreatedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken);
                return (true, $"You pick up {obj.ItemKey}.");
            }

            case WorldObjectKind.Chest:
            {
                if (obj.IsOpened)
                    return (false, "The chest is already open.");

                if (!string.IsNullOrWhiteSpace(obj.RequiredItemKey))
                {
                    var has = await _db.RunInventoryItems
                        .AnyAsync(i => i.RunId == run.RunId && i.ItemKey == obj.RequiredItemKey && i.Quantity > 0, cancellationToken);

                    if (!has)
                        return (false, $"The lock won't budge. You need '{obj.RequiredItemKey}'.");
                }

                obj.IsOpened = true;
                _db.WorldObjects.Update(obj);

                if (obj.LootItemKeys.Count == 0)
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    return (true, "You open the chest. It's empty.");
                }

                foreach (var loot in obj.LootItemKeys)
                    await AddToInventoryAsync(run, loot, 1, cancellationToken);

                var found = string.Join(", ", obj.LootItemKeys);

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    EventType = "object.open",
                    Message = $"You open a chest and find: {found}.",
                    CreatedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken);
                return (true, $"You open the chest and find: {found}.");
            }

            case WorldObjectKind.Trap:
            {
                if (obj.IsDisarmed)
                    return (false, "It's already been dealt with.");

                // No disarm mechanics yet; interacting triggers it.
                obj.IsTriggered = true;
                obj.IsDisarmed = true;
                _db.WorldObjects.Update(obj);

                run.Health += obj.HealthDelta;
                run.Sanity += obj.SanityDelta;

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    EventType = "trap.trigger",
                    Message = "You set off a trap.",
                    CreatedUtc = DateTime.UtcNow
                });

                await SaveRunAsync(run, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                return (true, "Pain flares. A trap snaps and then falls silent.");
            }

            case WorldObjectKind.PuzzleGate:
            {
                if (obj.IsSolved)
                    return (false, "It's already been overcome.");

                if (string.IsNullOrWhiteSpace(obj.RequiredItemKey))
                    return (false, "You can't make sense of it.");

                var has = await _db.RunInventoryItems
                    .AnyAsync(i => i.RunId == run.RunId && i.ItemKey == obj.RequiredItemKey && i.Quantity > 0, cancellationToken);

                if (!has)
                    return (false, $"You need '{obj.RequiredItemKey}'.");

                obj.IsSolved = true;
                _db.WorldObjects.Update(obj);

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    EventType = "puzzle.solve",
                    Message = $"You overcome: {obj.Name}.",
                    CreatedUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken);
                return (true, "Something yields. The way is open.");
            }

            case WorldObjectKind.Campfire:
            {
                var beforeHealth = run.Health;
                var beforeSanity = run.Sanity;
                run.Health = Math.Min(100, run.Health + 10);
                run.Sanity = Math.Min(100, run.Sanity + 15);

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    EventType = "campfire.rest",
                    Message = $"You rest by the fire (+{run.Health - beforeHealth} health, +{run.Sanity - beforeSanity} sanity).",
                    CreatedUtc = DateTime.UtcNow
                });

                await SaveRunAsync(run, cancellationToken);
                return (true, "Warmth steadies your breathing. The Night steps back.");
            }
        }

        // Fallback for kinds not handled here
        return (false, "Nothing happens.");
    }

    public async Task<(bool ok, string? message)> SearchRoomAsync(RunState run,
        CancellationToken cancellationToken = default)
    {
        var room = await GetCurrentRoomAsync(run, cancellationToken);
        if (room is null)
            return (false, "Your current room couldn't be loaded.");

        if (room.HasBeenSearched)
            return (false, "You've already searched this room.");

        room.HasBeenSearched = true;

        // Reveal hidden objects.
        var hidden = await this.GetHiddenWorldObjectsInCurrentRoomAsync(run, cancellationToken);
        foreach (var obj in hidden)
            obj.IsHidden = false;

        if (hidden.Count > 0)
            _db.WorldObjects.UpdateRange(hidden);

        // Trigger traps that fire on search.
        var trap = await _db.WorldObjects
            .Where(o => o.RunId == run.RunId && o.RoomId == room.RoomId && o.Kind == WorldObjectKind.Trap)
            .FirstOrDefaultAsync(o => o.TrapTrigger == TrapTrigger.OnSearchRoom && !o.IsDisarmed, cancellationToken);

        if (trap is not null)
        {
            trap.IsTriggered = true;
            trap.IsDisarmed = true;
            _db.WorldObjects.Update(trap);

            run.Health += trap.HealthDelta;
            run.Sanity += trap.SanityDelta;

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "trap.trigger",
                Message = "A trap triggers as you search.",
                CreatedUtc = DateTime.UtcNow
            });
        }

        // Legacy loot pickup (RoomInstance.Loot) is deprecated.
        // New content should flow through WorldObjectInstance (ground items/chests/caches) so UI and rules are consistent.
        if (room.Loot.Count > 0)
        {
            foreach (var item in room.Loot)
                await AddToInventoryAsync(run, item, 1, cancellationToken);

            var found = string.Join(", ", room.Loot);
            room.Loot.Clear();

            _db.RoomInstances.Update(room);

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "player.search",
                Message = $"You search and find: {found}.",
                CreatedUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            await SaveRunAsync(run, cancellationToken);

            // Mention reveal too, if it happened.
            if (hidden.Count > 0)
                return (true, $"You search carefully and find: {found}. You also notice something new.");

            return (true, $"You search carefully and find: {found}.");
        }

        // If search revealed something, don't penalize sanity as harshly.
        if (hidden.Count == 0 && trap is null)
            run.Sanity = Math.Max(0, run.Sanity - 1);

        _db.RoomInstances.Update(room);
        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "player.search",
            Message = hidden.Count > 0
                ? "You search and notice something you missed."
                : "You search every corner. Nothing. Just the feeling you're not alone.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await SaveRunAsync(run, cancellationToken);

        if (hidden.Count > 0)
            return (true, "You search around and notice something you missed.");

        return (true, "You search every corner. Nothing. Just the feeling you're not alone.");
    }

    public async Task<List<ActorInstance>> GetActorsInCurrentRoomAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.ActorInstances
            .Where(a => a.RunId == run.RunId && a.CurrentRoomId == run.CurrentRoomId && a.IsAlive)
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Ensure a simple encounter trigger on room entry: sometimes spawn an unknown actor.
    /// This is intentionally minimal and will evolve into the full encounter pipeline.
    /// </summary>
    public async Task<(ActorInstance? actor, string? dialogueNodeKey)> TriggerRoomEntryEncounterAsync(
        RunState run,
        CancellationToken cancellationToken = default)
    {
        // Don't spawn if an actor is already here.
        var existing = await GetActorsInCurrentRoomAsync(run, cancellationToken);
        if (existing.Count > 0)
            return (null, null);

        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, run.CurrentRoomId.GetHashCode()));

        var room = await GetCurrentRoomAsync(run, cancellationToken);
        var depth = room?.Depth ?? 0;
        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);

        // Base 35%, scaled by difficulty and depth.
        var baseChance = 0.35;
        var depthBonus = Math.Clamp(depth * 0.015, 0.0, 0.35); // up to +35%
        var chance = Math.Clamp(baseChance * diff.EnemySpawnMultiplier + depthBonus, 0.05, 0.90);

        if (rng.NextDouble() > chance)
            return (null, null);

        var isEnemy = rng.NextDouble() < 0.55;
        var actor = new ActorInstance
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            CurrentRoomId = run.CurrentRoomId,
            Kind = isEnemy ? ActorKind.Enemy : ActorKind.Npc,
            Name = isEnemy ? "Stranger" : "Stranger",
            Intensity = rng.Next(20, 75),
            // Actor morality is independent; player morality affects how encounters play out later.
            Morality = rng.Next(-50, 51),
            Sanity = rng.Next(40, 101),
            Disposition = ActorDisposition.Unknown,
            IsHostile = isEnemy && rng.NextDouble() < 0.6,
            IsPacified = false,
            IsAlive = true
        };

        _db.ActorInstances.Add(actor);

        // Create dialogue state for this actor if absent.
        var state = new RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actor.Id,
            CurrentNodeKey = "encounter.stranger.1",
            UpdatedUtc = DateTime.UtcNow
        };
        _db.RunDialogueStates.Add(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "encounter.spawn",
            Message = $"An unknown presence stirs here: {actor.Name}.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return (actor, state.CurrentNodeKey);
    }

    public async Task<(DialogueNode? node, List<DialogueChoice> choices, string? error)> GetDialogueAsync(
        RunState run,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.RunDialogueStates
            .FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actorId, cancellationToken);

        if (state is null)
            return (null, new List<DialogueChoice>(), "No dialogue state exists for this actor.");

        var node = await _db.DialogueNodes.FirstOrDefaultAsync(n => n.Key == state.CurrentNodeKey, cancellationToken);
        if (node is null)
            return (null, new List<DialogueChoice>(), $"Dialogue node '{state.CurrentNodeKey}' missing.");

        var allChoices = await _db.DialogueChoices
            .Where(c => c.FromNodeKey == state.CurrentNodeKey)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var available = allChoices
            .Where(c => IsChoiceAvailable(run, c))
            .ToList();

        return (node, available, null);
    }

    public async Task<(bool ok, string? message)> ChooseDialogueAsync(
        RunState run,
        Guid actorId,
        Guid choiceId,
        CancellationToken cancellationToken = default)
    {
        var choice = await _db.DialogueChoices.FirstOrDefaultAsync(c => c.Id == choiceId, cancellationToken);
        if (choice is null)
            return (false, "Choice not found.");

        return await ChooseDialogueAsync(run, actorId, choice, cancellationToken);
    }

    public async Task<(bool ok, string? message)> ChooseDialogueAsync(
        RunState run,
        Guid actorId,
        DialogueChoice choice,
        CancellationToken cancellationToken = default)
    {
        var actor = await _db.ActorInstances
            .FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == actorId, cancellationToken);

        if (actor is null)
            return (false, "Actor not found.");

        // Apply consequences.
        run.Sanity += choice.SanityDelta;
        run.Health += choice.HealthDelta;
        run.Morality += choice.MoralityDelta;

        if (choice.RevealDisposition is not null)
            actor.Disposition = choice.RevealDisposition.Value;

        if (choice.PacifyTarget && actor.Kind == ActorKind.Enemy)
        {
            actor.IsPacified = true;
            actor.IsHostile = false;
            actor.Disposition = ActorDisposition.Friendly;
        }

        // Advance dialogue state.
        var state = await _db.RunDialogueStates
            .FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actorId, cancellationToken);

        if (state is null)
            return (false, "Dialogue state not found.");

        if (string.IsNullOrWhiteSpace(choice.ToNodeKey))
        {
            // Conversation ends.
            _db.RunDialogueStates.Remove(state);
        }
        else
        {
            state.CurrentNodeKey = choice.ToNodeKey;
            state.UpdatedUtc = DateTime.UtcNow;
            _db.RunDialogueStates.Update(state);
        }

        _db.ActorInstances.Update(actor);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "dialogue.choice",
            Message = $"You chose: {choice.Text}",
            CreatedUtc = DateTime.UtcNow
        });

        await SaveRunAsync(run, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<(bool started, string? message)> TryStartNextStoryChapterAsync(RunState run,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.RunStoryStates.FirstOrDefaultAsync(s => s.RunId == run.RunId, cancellationToken);
        if (state is null)
        {
            state = new RunStoryState
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActiveChapterKey = null,
                CurrentNodeKey = null,
                UpdatedUtc = DateTime.UtcNow
            };
            _db.RunStoryStates.Add(state);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // If a chapter is already active, don't start another.
        if (!string.IsNullOrWhiteSpace(state.ActiveChapterKey) && !string.IsNullOrWhiteSpace(state.CurrentNodeKey))
            return (false, null);

        // Find the next chapter not yet completed.
        var completed = state.CompletedChapterKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var next = await _db.StoryChapters
            .OrderBy(c => c.Order)
            .FirstOrDefaultAsync(c => !completed.Contains(c.Key), cancellationToken);

        if (next is null)
            return (false, null);

        state.ActiveChapterKey = next.Key;
        state.CurrentNodeKey = next.StartNodeKey;
        state.UpdatedUtc = DateTime.UtcNow;
        _db.RunStoryStates.Update(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "story.chapter.start",
            Message = $"Chapter started: {next.Title}",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (true, next.Title);
    }

    public async Task<(DialogueNode? node, List<DialogueChoice> choices, string? error)> GetActiveStoryDialogueAsync(
        RunState run,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.RunStoryStates.FirstOrDefaultAsync(s => s.RunId == run.RunId, cancellationToken);
        if (state is null || string.IsNullOrWhiteSpace(state.CurrentNodeKey))
            return (null, new List<DialogueChoice>(), "No active story chapter.");

        var node = await _db.DialogueNodes.FirstOrDefaultAsync(n => n.Key == state.CurrentNodeKey, cancellationToken);
        if (node is null)
            return (null, new List<DialogueChoice>(), $"Dialogue node '{state.CurrentNodeKey}' missing.");

        var allChoices = await _db.DialogueChoices
            .Where(c => c.FromNodeKey == state.CurrentNodeKey)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var available = allChoices
            .Where(c => IsChoiceAvailable(run, c))
            .ToList();

        return (node, available, null);
    }

    public async Task<(bool ok, bool chapterEnded, string? message)> ChooseActiveStoryDialogueAsync(
        RunState run,
        Guid choiceId,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.RunStoryStates.FirstOrDefaultAsync(s => s.RunId == run.RunId, cancellationToken);
        if (state is null || string.IsNullOrWhiteSpace(state.CurrentNodeKey) || string.IsNullOrWhiteSpace(state.ActiveChapterKey))
            return (false, false, "No active story chapter.");

        var choice = await _db.DialogueChoices.FirstOrDefaultAsync(c => c.Id == choiceId, cancellationToken);
        if (choice is null)
            return (false, false, "Choice not found.");

        // Apply consequences onto the player.
        run.Sanity += choice.SanityDelta;
        run.Health += choice.HealthDelta;
        run.Morality += choice.MoralityDelta;

        // Advance or end chapter.
        if (string.IsNullOrWhiteSpace(choice.ToNodeKey))
        {
            // Chapter ends now.
            state.CompletedChapterKeys.Add(state.ActiveChapterKey);
            state.ActiveChapterKey = null;
            state.CurrentNodeKey = null;
            state.UpdatedUtc = DateTime.UtcNow;

            _db.RunStoryStates.Update(state);

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "story.chapter.end",
                Message = "A story chapter ended.",
                CreatedUtc = DateTime.UtcNow
            });

            await SaveRunAsync(run, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return (true, true, null);
        }

        state.CurrentNodeKey = choice.ToNodeKey;
        state.UpdatedUtc = DateTime.UtcNow;
        _db.RunStoryStates.Update(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "story.choice",
            Message = $"Story choice: {choice.Text}",
            CreatedUtc = DateTime.UtcNow
        });

        await SaveRunAsync(run, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, false, null);
    }

    private static bool IsChoiceAvailable(RunState run, DialogueChoice choice)
    {
        if (choice.RequireMinMorality is not null && run.Morality < choice.RequireMinMorality.Value)
            return false;
        if (choice.RequireMaxMorality is not null && run.Morality > choice.RequireMaxMorality.Value)
            return false;
        if (choice.RequireMinSanity is not null && run.Sanity < choice.RequireMinSanity.Value)
            return false;

        return true;
    }

    private static string NormalizePlayerName(string playerName)
    {
        playerName = playerName.Trim();
        return string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
    }

    private static void ClampRunStats(RunState run)
    {
        run.Sanity = Math.Clamp(run.Sanity, 0, 100);
        run.Health = Math.Clamp(run.Health, 0, 100);
        run.Morality = Math.Clamp(run.Morality, -100, 100);
    }

    private static string GetCreepyWhisper(Random rng)
    {
        var whispers = new[]
        {
            "You think the floor is level. It is not.",
            "Something counts your steps and forgets none.",
            "A door breathes when you look away.",
            "The light does not like you.",
            "Your name is smaller here.",
            "Every shadow knows where you sleep.",
            "The House is remembering you wrong."
        };
        return whispers[rng.Next(0, whispers.Length)];
    }

    private static Guid DeterministicGuid(Guid runId, string scope, int index)
    {
        var bytes = new byte[16];
        var hash = HashCode.Combine(runId, scope, index);
        BitConverter.GetBytes(hash).CopyTo(bytes, 0);
        BitConverter.GetBytes(HashCode.Combine(hash, index * 31)).CopyTo(bytes, 4);
        BitConverter.GetBytes(HashCode.Combine(hash, scope.Length)).CopyTo(bytes, 8);
        BitConverter.GetBytes(HashCode.Combine(hash, 0x71ED)).CopyTo(bytes, 12);
        return new Guid(bytes);
    }

    private static Guid DeterministicGuidFromSeed(int seed)
    {
        // Seed-only deterministic RunId so that "same seed" truly reproduces topology regardless of player name.
        // This is important for tests and for roguelike seed sharing.
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        BitConverter.GetBytes(HashCode.Combine(seed, 1)).CopyTo(bytes, 4);
        BitConverter.GetBytes(HashCode.Combine(seed, 2)).CopyTo(bytes, 8);
        BitConverter.GetBytes(HashCode.Combine(seed, 3)).CopyTo(bytes, 12);
        return new Guid(bytes);
    }
}

