using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
using EndlessNight.Domain.Story;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed partial class RunService
{
    // ...existing code...

    public async Task<WorldObjectInstance?> GetPendingEntryTrapAsync(RunState run, CancellationToken cancellationToken = default)
    {
        // Only traps that trigger on entry and haven't been resolved.
        return await _db.WorldObjects
            .Where(o => o.RunId == run.RunId && o.RoomId == run.CurrentRoomId)
            .Where(o => o.Kind == WorldObjectKind.Trap)
            .Where(o => o.TrapTrigger == TrapTrigger.OnEnterRoom)
            .Where(o => !o.IsDisarmed && !o.IsTriggered)
            .OrderBy(o => o.Key)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool ok, string message)> ResolveEntryTrapAsync(RunState run, Guid trapId, bool disarm,
        CancellationToken cancellationToken = default)
    {
        var trap = await _db.WorldObjects.FirstOrDefaultAsync(o => o.RunId == run.RunId && o.Id == trapId, cancellationToken);
        if (trap is null || trap.Kind != WorldObjectKind.Trap)
            return (false, "That trap isn't here.");

        if (trap.RoomId != run.CurrentRoomId)
            return (false, "You're not in that room anymore.");

        if (trap.TrapTrigger != TrapTrigger.OnEnterRoom)
            return (false, "That doesn't trigger on entry.");

        if (trap.IsDisarmed || trap.IsTriggered)
            return (false, "It's already been dealt with.");

        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);

        if (disarm)
        {
            var cost = Math.Max(0, diff.TrapDisarmSanityCost);
            run.Sanity = Math.Max(0, run.Sanity - cost);
            trap.IsDisarmed = true;

            _db.WorldObjects.Update(trap);
            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                EventType = "trap.disarm.entry",
                Message = $"You steady your breathing and disarm: {trap.Name} (Sanity -{cost}).",
                CreatedUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            await SaveRunAsync(run, cancellationToken);
            return (true, $"Disarmed {trap.Name} (Sanity -{cost}).");
        }

        // Endure trigger
        trap.IsTriggered = true;
        trap.IsDisarmed = true;
        _db.WorldObjects.Update(trap);

        var beforeH = run.Health;
        var beforeS = run.Sanity;
        run.Health = Math.Clamp(run.Health + trap.HealthDelta, 0, 100);
        run.Sanity = Math.Clamp(run.Sanity + trap.SanityDelta, 0, 100);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = "trap.trigger.entry",
            Message = $"{trap.Name} snaps: Health {run.Health - beforeH}, Sanity {run.Sanity - beforeS}.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await SaveRunAsync(run, cancellationToken);
        return (true, $"{trap.Name} triggers.");
    }

    public async Task<int> GetPacifySanityCostAsync(RunState run, ActorInstance enemy, CancellationToken cancellationToken = default)
    {
        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);
        var baseCost = Math.Max(0, diff.PacifyBaseSanityCost);
        var mult = Math.Max(0.1f, diff.PacifyCostMultiplier);

        // Light intensity scaling for Undertale-like "harder monster" feel.
        var intensityFactor = 1.0f + (Math.Clamp(enemy.Intensity, 0, 100) / 100.0f) * 0.5f; // 1.0..1.5
        var cost = (int)MathF.Ceiling(baseCost * mult * (float)intensityFactor);
        return Math.Clamp(cost, 0, 100);
    }
    
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

        // On-entry encounter chance (difficulty-driven).
        await TriggerRoomEntryEncounterAsync(run, cancellationToken);

        // If NPCs are present, they may speak automatically on entry (in SpawnIndex order).
        await TriggerNpcAutoSpeakOnEntryAsync(run, cancellationToken);

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

        // Per-turn pulse: actors may spawn/move.
        await ProcessActorTurnPulseAsync(run, cancellationToken);

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

                // Taking an item consumes a turn.
                run.Turn++;
                await ProcessActorTurnPulseAsync(run, cancellationToken);
                await SaveRunAsync(run, cancellationToken);

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

                // Opening a chest consumes a turn.
                run.Turn++;
                await ProcessActorTurnPulseAsync(run, cancellationToken);
                await SaveRunAsync(run, cancellationToken);

                return (true, obj.LootItemKeys.Count == 0 ? "You open the chest. It's empty." : $"You open the chest and find: {found}.");
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

                run.Turn++;
                await ProcessActorTurnPulseAsync(run, cancellationToken);
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

                // Solving consumes a turn.
                run.Turn++;
                await ProcessActorTurnPulseAsync(run, cancellationToken);
                await SaveRunAsync(run, cancellationToken);

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

                // Resting consumes a turn.
                run.Turn++;
                await ProcessActorTurnPulseAsync(run, cancellationToken);
                await SaveRunAsync(run, cancellationToken);
                return (true, "Warmth steadies your breathing. The Night steps back.");
            }
        }

        // Fallback for kinds not handled here
        return (false, "Nothing happens.");
    }

    public async Task<(bool ok, string? error)> SearchRoomAsync(RunState run,
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
            .OrderBy(a => a.SpawnIndex)
            .ThenBy(a => a.Kind)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActorInstance>> GetEnemiesInCurrentRoomAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.ActorInstances
            .Where(a => a.RunId == run.RunId && a.CurrentRoomId == run.CurrentRoomId)
            .Where(a => a.IsAlive && a.Kind == ActorKind.Enemy)
            .OrderBy(a => a.SpawnIndex)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActorInstance>> GetNpcsInCurrentRoomAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.ActorInstances
            .Where(a => a.RunId == run.RunId && a.CurrentRoomId == run.CurrentRoomId)
            .Where(a => a.IsAlive && a.Kind == ActorKind.Npc)
            .OrderBy(a => a.SpawnIndex)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task TriggerNpcAutoSpeakOnEntryAsync(RunState run, CancellationToken cancellationToken = default)
    {
        var npcs = await GetNpcsInCurrentRoomAsync(run, cancellationToken);
        if (npcs.Count == 0)
            return;

        // Per requirements: when configured, NPCs will speak in order of their index.
        // "Configured" = AutoSpeakOnEnter. We still allow a small chance for ambient chatter.
        var cfg = await _db.RunConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.RunId == run.RunId, cancellationToken);
        var enabledPacks = (IReadOnlyList<string>)(cfg?.EnabledLorePacks ?? new List<string>);

        var room = await GetCurrentRoomAsync(run, cancellationToken);
        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, run.CurrentRoomId.GetHashCode(), 50501));

        foreach (var npc in npcs)
        {
            var shouldSpeak = npc.AutoSpeakOnEnter || rng.NextDouble() < 0.15; // small ambient chance
            if (!shouldSpeak)
                continue;

            // Ensure dialogue state exists
            var state = await _db.RunDialogueStates.FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == npc.Id, cancellationToken);
            if (state is null)
            {
                state = new RunDialogueState
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    ActorId = npc.Id,
                    CurrentNodeKey = "encounter.stranger.1",
                    ConversationPhase = "opening",
                    UpdatedUtc = DateTime.UtcNow
                };
                _db.RunDialogueStates.Add(state);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // Compose one procedural line and log it as "npc.auto".
            var contextTags = new List<string> { "npc", "auto", "encounter" };
            if (room?.RoomTags is not null) contextTags.AddRange(room.RoomTags);

            var composer = new ProceduralDialogueComposer(_db);
            var composed = await composer.ComposeAsync(new ProceduralDialogueComposer.ComposeRequest(
                RunId: run.RunId,
                Seed: run.Seed,
                Turn: run.Turn,
                PlayerName: run.PlayerName,
                RoomName: room?.Name ?? "",
                EnabledLorePacks: enabledPacks,
                ContextTags: contextTags,
                Sanity: run.Sanity,
                Morality: run.Morality,
                Disposition: npc.Disposition,
                MaxLines: 1,
                SeedOffset: cfg?.DialogueSeedOffset,
                Phase: "opening"
            ), cancellationToken);

            if (string.IsNullOrWhiteSpace(composed.Text))
                continue;

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActorId = npc.Id,
                Turn = run.Turn,
                EventType = "npc.auto",
                Message = composed.Text,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsPacifyUnlockedAsync(RunState run, Guid enemyId, CancellationToken cancellationToken = default)
    {
        var enemy = await _db.ActorInstances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == enemyId, cancellationToken);
        if (enemy is null || enemy.Kind != ActorKind.Enemy || !enemy.IsAlive)
            return false;
        return enemy.PacifyUnlocked;
    }

    public async Task<(bool ok, string message)> TalkToEnemyForPacifyProgressAsync(RunState run, Guid enemyId,
        CancellationToken cancellationToken = default)
    {
        var enemy = await _db.ActorInstances.FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == enemyId, cancellationToken);
        if (enemy is null || enemy.Kind != ActorKind.Enemy || !enemy.IsAlive)
            return (false, "That enemy isn't here.");

        if (enemy.CurrentRoomId != run.CurrentRoomId)
            return (false, "It's not in this room.");

        // Talking costs a tiny amount of time (turn pulse) but not necessarily sanity.
        // It does progress toward pacify.
        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, enemy.Id.GetHashCode(), 91919));

        // Increase progress more if the enemy is "simple" or less intense.
        var speech = Math.Clamp(enemy.SpeechLevel, 0, 2);
        var intensity01 = Math.Clamp(enemy.Intensity / 100.0, 0.0, 1.0);
        var baseGain = speech == 0 ? 45 : speech == 1 ? 35 : 25;
        var gain = (int)Math.Round(baseGain * (1.0 - intensity01 * 0.35) + rng.Next(0, 6));

        enemy.PacifyProgress = Math.Clamp(enemy.PacifyProgress + gain, 0, 100);
        if (!enemy.PacifyUnlocked && enemy.PacifyProgress >= 100)
        {
            enemy.PacifyUnlocked = true;
            enemy.Disposition = ActorDisposition.Friendly;

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActorId = enemy.Id,
                Turn = run.Turn,
                EventType = "enemy.pacify.unlock",
                Message = $"You find the right rhythm. {enemy.Name} hesitates — you might be able to calm it.",
                CreatedUtc = DateTime.UtcNow
            });
        }

        _db.ActorInstances.Update(enemy);
        await _db.SaveChangesAsync(cancellationToken);

        // One procedural "talk" line for flavor.
        var state = await _db.RunDialogueStates.FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == enemy.Id, cancellationToken);
        if (state is null)
        {
            state = new RunDialogueState
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActorId = enemy.Id,
                CurrentNodeKey = "encounter.stranger.1",
                ConversationPhase = "opening",
                UpdatedUtc = DateTime.UtcNow
            };
            _db.RunDialogueStates.Add(state);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Tag enemy talk by complexity so the composer can pick appropriate snippet pools.
        // (This is non-LLM and deterministic, but feels more alive due to variety.)
        await TryComposeProceduralDialogueAsync(run, enemy, state, cancellationToken);

        // Talking consumes a turn to keep pressure on.
        await AdvanceTurnAsync(run, "enemy.talk", cancellationToken);

        if (enemy.PacifyUnlocked)
            return (true, "You talk it down enough to see a path to mercy. (Pacify unlocked)");

        return (true, $"You speak carefully. Something shifts. (Pacify {enemy.PacifyProgress}% )");
    }

    public async Task<(bool ok, string message)> TryPacifyEnemyAsync(RunState run, Guid enemyId, CancellationToken cancellationToken = default)
    {
        var enemy = await _db.ActorInstances.FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == enemyId, cancellationToken);
        if (enemy is null || enemy.Kind != ActorKind.Enemy || !enemy.IsAlive)
            return (false, "That enemy isn't here.");

        if (enemy.CurrentRoomId != run.CurrentRoomId)
            return (false, "It's not in this room.");

        if (!enemy.PacifyUnlocked)
            return (false, "You don't know what to say yet. Try talking to it.");

        var cost = await GetPacifySanityCostAsync(run, enemy, cancellationToken);
        if (run.Sanity < cost)
            return (false, $"You can't focus enough (need {cost} sanity). ");

        run.Sanity = Math.Max(0, run.Sanity - cost);

        // Despawn: mark not alive and pacified.
        enemy.IsPacified = true;
        enemy.IsAlive = false;
        _db.ActorInstances.Update(enemy);

        // Remove active dialogue state so it doesn't linger.
        var state = await _db.RunDialogueStates.FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == enemy.Id, cancellationToken);
        if (state is not null)
            _db.RunDialogueStates.Remove(state);

        // Mark room as cleared (prevents new enemy spawns here).
        var room = await _db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == run.CurrentRoomId, cancellationToken);
        if (room is not null)
        {
            room.IsCleared = true;
            _db.RoomInstances.Update(room);
        }

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            ActorId = enemy.Id,
            EventType = "enemy.pacify",
            Message = $"You reach for mercy. The {enemy.Name} relents and fades (Sanity -{cost}).",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await SaveRunAsync(run, cancellationToken);
        return (true, $"Pacified {enemy.Name} (Sanity -{cost}).");
    }

    /// <summary>
    /// Ensure a simple encounter trigger on room entry: sometimes spawn an unknown actor.
    /// This is intentionally minimal and will evolve into the full encounter pipeline.
    /// </summary>
    public async Task<(ActorInstance? actor, string? dialogueNodeKey)> TriggerRoomEntryEncounterAsync(
        RunState run,
        CancellationToken cancellationToken = default)
    {
        // Don't spawn if an actor is already here beyond caps; this keeps rooms readable.
        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);
        var counts = await GetActorCountsInRoomAsync(run.RunId, run.CurrentRoomId, cancellationToken);
        if (counts.Npcs + counts.Enemies >= Math.Max(0, diff.MaxNpcsPerRoom) + Math.Max(0, diff.MaxEnemiesPerRoom))
            return (null, null);

        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, run.CurrentRoomId.GetHashCode(), 121));

        var chance = Clamp01(diff.ActorSpawnChanceOnEntry);
        if (rng.NextDouble() > chance)
            return (null, null);

        var actor = await TrySpawnActorInRoomAsync(run, run.CurrentRoomId, triggerEventType: "actor.spawn.entry", cancellationToken);
        if (actor is null)
            return (null, null);

        // Keep returning a node key for consumers that want to open dialogue.
        return (actor, "encounter.stranger.1");
    }

    private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    private sealed record RoomActorCounts(int Npcs, int Enemies);

    private async Task<RoomActorCounts> GetActorCountsInRoomAsync(Guid runId, Guid roomId, CancellationToken cancellationToken)
    {
        var actors = await _db.ActorInstances
            .AsNoTracking()
            .Where(a => a.RunId == runId && a.CurrentRoomId == roomId && a.IsAlive)
            .Select(a => a.Kind)
            .ToListAsync(cancellationToken);

        var npcs = actors.Count(k => k == ActorKind.Npc);
        var enemies = actors.Count(k => k == ActorKind.Enemy);
        return new RoomActorCounts(npcs, enemies);
    }

    private static ActorKind ChooseActorKind(Random rng, DifficultyProfile diff)
    {
        // Weighted random using difficulty multipliers.
        // Lower difficulties seed more NPCs; higher difficulties tend toward enemies.
        var npcWeight = Math.Max(0.01, diff.NpcSpawnMultiplier);
        var enemyWeight = Math.Max(0.01, diff.EnemySpawnMultiplier);

        var roll = rng.NextDouble() * (npcWeight + enemyWeight);
        return roll < npcWeight ? ActorKind.Npc : ActorKind.Enemy;
    }

    private ActorInstance CreateNpc(Guid runId, Guid roomId, Random rng)
    {
        var names = new[]
        {
            "Wanderer", "Lost Soul", "Muffled Voice", "Hushed Traveler", "Flicker", "Quiet Stranger"
        };

        return new ActorInstance
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            CurrentRoomId = roomId,
            Kind = ActorKind.Npc,
            Name = names[rng.Next(names.Length)],
            Intensity = rng.Next(10, 55),
            Morality = rng.Next(-25, 76),
            Sanity = rng.Next(40, 101),
            Disposition = ActorDisposition.Unknown,
            IsHostile = false,
            IsPacified = false,
            IsAlive = true,
            // New defaults
            SpawnIndex = rng.Next(1, 10_000),
            AutoSpeakOnEnter = rng.NextDouble() < 0.20,
            SpeechLevel = rng.NextDouble() < 0.20 ? 2 : 1,
            PacifyProgress = 0,
            PacifyUnlocked = true
        };
    }

    private ActorInstance CreateEnemy(Guid runId, Guid roomId, Random rng)
    {
        var names = new[]
        {
            "Stranger", "Hollow", "Watcher", "Pale Thing", "Shade", "Grinning Silence"
        };

        // Speech level: most enemies are simple; a few are advanced.
        var advanced = rng.NextDouble() < 0.18;

        return new ActorInstance
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            CurrentRoomId = roomId,
            Kind = ActorKind.Enemy,
            Name = names[rng.Next(names.Length)],
            Intensity = rng.Next(20, 80),
            Morality = rng.Next(-75, 26),
            Sanity = rng.Next(0, 51),
            Disposition = ActorDisposition.Unknown,
            IsHostile = rng.NextDouble() < 0.65,
            IsPacified = false,
            IsAlive = true,
            // New defaults
            SpawnIndex = rng.Next(1, 10_000),
            AutoSpeakOnEnter = false,
            SpeechLevel = advanced ? 2 : 0,
            PacifyProgress = 0,
            PacifyUnlocked = false
        };
    }

    public async Task<ActorInstance> ForceSpawnActorInCurrentRoomAsync(RunState run, ActorKind kind,
        CancellationToken cancellationToken = default)
    {
        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, run.CurrentRoomId.GetHashCode(), (int)kind, 771));
        var actor = kind == ActorKind.Npc
            ? CreateNpc(run.RunId, run.CurrentRoomId, rng)
            : CreateEnemy(run.RunId, run.CurrentRoomId, rng);

        _db.ActorInstances.Add(actor);

        var state = new RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actor.Id,
            CurrentNodeKey = "encounter.stranger.1",
            ConversationPhase = "opening",
            UpdatedUtc = DateTime.UtcNow
        };
        _db.RunDialogueStates.Add(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            ActorId = actor.Id,
            EventType = "actor.spawn.force",
            Message = $"(Test) {actor.Kind} spawned: {actor.Name}.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await TryComposeProceduralDialogueAsync(run, actor, state, cancellationToken);
        return actor;
    }

    public async Task AdvanceTurnAsync(RunState run, string sourceEventType,
        CancellationToken cancellationToken = default)
    {
        // Ensure stats are clamped at the end.
        run.Turn++;

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            EventType = sourceEventType,
            Message = "Time passes.",
            CreatedUtc = DateTime.UtcNow
        });

        await ProcessActorTurnPulseAsync(run, cancellationToken);
        await SaveRunAsync(run, cancellationToken);
    }

    public async Task PopulateActorsEveryRoomAsync(RunState run, float npcBias01 = 0.6f,
        CancellationToken cancellationToken = default)
    {
        npcBias01 = Math.Clamp(npcBias01, 0f, 1f);

        var rooms = await _db.RoomInstances
            .AsNoTracking()
            .Where(r => r.RunId == run.RunId)
            .Select(r => r.RoomId)
            .ToListAsync(cancellationToken);

        if (rooms.Count == 0)
            return;

        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);
        var rng = new Random(HashCode.Combine(run.Seed, run.RunId.GetHashCode(), 424242));

        foreach (var roomId in rooms)
        {
            // Skip if already has any actor.
            var hasAny = await _db.ActorInstances
                .AsNoTracking()
                .AnyAsync(a => a.RunId == run.RunId && a.CurrentRoomId == roomId && a.IsAlive, cancellationToken);
            if (hasAny)
                continue;

            var kind = rng.NextDouble() < npcBias01 ? ActorKind.Npc : ActorKind.Enemy;
            var actor = kind == ActorKind.Npc
                ? CreateNpc(run.RunId, roomId, rng)
                : CreateEnemy(run.RunId, roomId, rng);

            _db.ActorInstances.Add(actor);

            _db.RunDialogueStates.Add(new RunDialogueState
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActorId = actor.Id,
                CurrentNodeKey = "encounter.stranger.1",
                ConversationPhase = "opening",
                UpdatedUtc = DateTime.UtcNow
            });

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                Turn = run.Turn,
                ActorId = actor.Id,
                EventType = "test.populate",
                Message = $"(Test) Populated room with {actor.Kind}: {actor.Name}.",
                CreatedUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Compose one procedural line for any newly created dialogue states.
        var newStates = await _db.RunDialogueStates
            .Where(s => s.RunId == run.RunId && s.ConversationPhase == "opening")
            .ToListAsync(cancellationToken);

        foreach (var state in newStates)
        {
            var actor = await _db.ActorInstances.FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == state.ActorId, cancellationToken);
            if (actor is null)
                continue;

            await TryComposeProceduralDialogueAsync(run, actor, state, cancellationToken);
        }
    }

    private async Task ProcessActorTurnPulseAsync(RunState run, CancellationToken cancellationToken)
    {
        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);

        // Ensure minimums in the player's current room (simple top-up).
        await EnsureRoomMinimumActorsAsync(run, run.CurrentRoomId, diff, cancellationToken);

        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, run.CurrentRoomId.GetHashCode(), 991));

        // Spawn pulse (25% by default)
        var spawnChance = Clamp01(diff.ActorSpawnChancePerTurn);
        if (rng.NextDouble() < spawnChance)
            await TrySpawnActorInRoomAsync(run, run.CurrentRoomId, triggerEventType: "actor.spawn.turn", cancellationToken);

        // Move pulse (25% by default). If it hits, each actor gets one move attempt.
        var moveChance = Clamp01(diff.ActorMoveChancePerTurn);
        if (rng.NextDouble() < moveChance)
            await TryMoveActorsOnceAsync(run, rng, cancellationToken);
    }

    private async Task EnsureRoomMinimumActorsAsync(RunState run, Guid roomId, DifficultyProfile diff, CancellationToken cancellationToken)
    {
        var counts = await GetActorCountsInRoomAsync(run.RunId, roomId, cancellationToken);

        var room = await _db.RoomInstances.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == roomId, cancellationToken);

        var needNpcs = Math.Max(0, diff.MinNpcsPerRoom - counts.Npcs);
        var needEnemies = room is not null && room.IsCleared
            ? 0
            : Math.Max(0, diff.MinEnemiesPerRoom - counts.Enemies);

        // Spawn deterministically based on seed/turn.
        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, roomId.GetHashCode(), 8801));

        for (int i = 0; i < needNpcs; i++)
            await TrySpawnSpecificKindAsync(run, roomId, ActorKind.Npc, "actor.spawn.min", rng, cancellationToken);

        for (int i = 0; i < needEnemies; i++)
            await TrySpawnSpecificKindAsync(run, roomId, ActorKind.Enemy, "actor.spawn.min", rng, cancellationToken);
    }

    private async Task<ActorInstance?> TrySpawnSpecificKindAsync(RunState run, Guid roomId, ActorKind kind, string eventType,
        Random rng, CancellationToken cancellationToken)
    {
        // Suppress new enemy spawns in cleared rooms.
        if (kind == ActorKind.Enemy)
        {
            var room = await _db.RoomInstances.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == roomId, cancellationToken);
            if (room is not null && room.IsCleared)
                return null;
        }

        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);
        var counts = await GetActorCountsInRoomAsync(run.RunId, roomId, cancellationToken);

        if (kind == ActorKind.Npc && counts.Npcs >= Math.Max(0, diff.MaxNpcsPerRoom))
            return null;
        if (kind == ActorKind.Enemy && counts.Enemies >= Math.Max(0, diff.MaxEnemiesPerRoom))
            return null;

        var actor = kind == ActorKind.Npc
            ? CreateNpc(run.RunId, roomId, rng)
            : CreateEnemy(run.RunId, roomId, rng);

        _db.ActorInstances.Add(actor);

        var state = new RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actor.Id,
            CurrentNodeKey = "encounter.stranger.1",
            ConversationPhase = "opening",
            UpdatedUtc = DateTime.UtcNow
        };
        _db.RunDialogueStates.Add(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            ActorId = actor.Id,
            EventType = eventType,
            Message = $"A presence is already here: {actor.Name}.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await TryComposeProceduralDialogueAsync(run, actor, state, cancellationToken);
        return actor;
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

    public async Task<(EndlessNight.Domain.Dialogue.DialogueNode? node, List<EndlessNight.Domain.Dialogue.DialogueChoice> choices, string? error)>
        GetDialogueAsync(RunState run, Guid actorId, CancellationToken cancellationToken = default)
    {
        var state = await _db.RunDialogueStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actorId, cancellationToken);

        if (state is null)
            return (null, new List<EndlessNight.Domain.Dialogue.DialogueChoice>(), null);

        var node = await _db.DialogueNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Key == state.CurrentNodeKey, cancellationToken);

        if (node is null)
            return (null, new List<EndlessNight.Domain.Dialogue.DialogueChoice>(), $"Dialogue node '{state.CurrentNodeKey}' not found.");

        var choices = await _db.DialogueChoices
            .AsNoTracking()
            .Where(c => c.FromNodeKey == state.CurrentNodeKey)
            .OrderBy(c => c.Text)
            .ToListAsync(cancellationToken);

        // Apply requirement gates.
        choices = choices.Where(c => IsChoiceAvailable(run, c)).ToList();

        return (node, choices, null);
    }

    public async Task<(bool ok, string? message)> ChooseDialogueAsync(
        RunState run,
        Guid actorId,
        EndlessNight.Domain.Dialogue.DialogueChoice choice,
        CancellationToken cancellationToken = default)
    {
        var state = await _db.RunDialogueStates
            .FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actorId, cancellationToken);

        if (state is null)
            return (false, "No active conversation.");

        if (!string.Equals(state.CurrentNodeKey, choice.FromNodeKey, StringComparison.Ordinal))
            return (false, "That choice doesn't match the current conversation.");

        if (!IsChoiceAvailable(run, choice))
            return (false, "You can't choose that right now.");

        // Apply deterministic consequences.
        run.Sanity += choice.SanityDelta;
        run.Health += choice.HealthDelta;
        run.Morality += choice.MoralityDelta;

        // Optional: grant an item.
        if (!string.IsNullOrWhiteSpace(choice.GrantItemKey))
        {
            var qty = choice.GrantItemQuantity <= 0 ? 1 : choice.GrantItemQuantity;
            await AddToInventoryAsync(run, choice.GrantItemKey!, qty, cancellationToken);
        }

        // Actor side-effects
        var actor = await _db.ActorInstances.FirstOrDefaultAsync(a => a.RunId == run.RunId && a.Id == actorId, cancellationToken);
        if (actor is not null)
        {
            if (choice.RevealDisposition is not null)
                actor.Disposition = choice.RevealDisposition.Value;

            if (choice.PacifyTarget && actor.Kind == ActorKind.Enemy)
            {
                // Difficulty-scaled pacify: spend extra sanity and despawn.
                var cost = await GetPacifySanityCostAsync(run, actor, cancellationToken);
                run.Sanity = Math.Max(0, run.Sanity - cost);

                actor.IsPacified = true;
                actor.IsAlive = false;

                // Mark room as cleared to suppress future enemy spawns.
                var room = await _db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == run.CurrentRoomId, cancellationToken);
                if (room is not null)
                {
                    room.IsCleared = true;
                    _db.RoomInstances.Update(room);
                }

                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    ActorId = actorId,
                    Turn = run.Turn,
                    EventType = "enemy.pacify",
                    Message = $"You choose mercy. It costs you (Sanity -{cost}).",
                    CreatedUtc = DateTime.UtcNow
                });
            }

            _db.ActorInstances.Update(actor);
        }

        // Advance or end.
        if (string.IsNullOrWhiteSpace(choice.ToNodeKey))
        {
            _db.RunDialogueStates.Remove(state);
        }
        else
        {
            state.CurrentNodeKey = choice.ToNodeKey!;
            state.UpdatedUtc = DateTime.UtcNow;
            _db.RunDialogueStates.Update(state);
        }

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actorId,
            Turn = run.Turn,
            EventType = "dialogue.choose",
            Message = choice.Text,
            CreatedUtc = DateTime.UtcNow
        });

        await SaveRunAsync(run, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public async Task<string?> GetLatestProceduralDialogueForActorAsync(RunState run, Guid actorId, CancellationToken cancellationToken = default)
    {
        var msg = await _db.RoomEventLogs
            .AsNoTracking()
            .Where(e => e.RunId == run.RunId && e.ActorId == actorId && e.EventType == "dialogue.procedural")
            .OrderByDescending(e => e.CreatedUtc)
            .Select(e => e.Message)
            .FirstOrDefaultAsync(cancellationToken);

        return msg;
    }

    public async Task<List<string>> GetNpcAutoTalkLinesForCurrentTurnAsync(RunState run, CancellationToken cancellationToken = default)
    {
        return await _db.RoomEventLogs
            .AsNoTracking()
            .Where(e => e.RunId == run.RunId && e.Turn == run.Turn && e.EventType == "npc.auto")
            .OrderBy(e => e.CreatedUtc)
            .Select(e => e.Message)
            .ToListAsync(cancellationToken);
    }

    private async Task TryMoveActorsOnceAsync(RunState run, Random rng, CancellationToken cancellationToken)
    {
        var actors = await _db.ActorInstances
            .Where(a => a.RunId == run.RunId && a.IsAlive)
            .ToListAsync(cancellationToken);

        if (actors.Count == 0)
            return;

        // Only enemies move for now (NPCs can be made mobile later).
        actors = actors.Where(a => a.Kind == ActorKind.Enemy).ToList();
        if (actors.Count == 0)
            return;

        var roomMap = await _db.RoomInstances
            .AsNoTracking()
            .Where(r => r.RunId == run.RunId)
            .ToDictionaryAsync(r => r.RoomId, r => r, cancellationToken);

        foreach (var actor in actors)
        {
            if (!roomMap.TryGetValue(actor.CurrentRoomId, out var room))
                continue;

            if (room.Exits.Count == 0)
                continue;

            // Follow bias: if actor has direct exit to player's room, take it ~50% of the time.
            Guid? nextRoomId = null;
            if (room.Exits.Values.Contains(run.CurrentRoomId) && rng.NextDouble() < 0.50)
                nextRoomId = run.CurrentRoomId;

            if (nextRoomId is null)
            {
                var exits = room.Exits.Values.Where(id => id != Guid.Empty).Distinct().ToList();
                if (exits.Count == 0)
                    continue;
                nextRoomId = exits[rng.Next(exits.Count)];
            }

            if (nextRoomId.Value == Guid.Empty || nextRoomId.Value == actor.CurrentRoomId)
                continue;

            var fromRoomId = actor.CurrentRoomId;
            actor.CurrentRoomId = nextRoomId.Value;
            _db.ActorInstances.Update(actor);

            if (actor.CurrentRoomId == run.CurrentRoomId || fromRoomId == run.CurrentRoomId)
            {
                _db.RoomEventLogs.Add(new RoomEventLog
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Turn = run.Turn,
                    ActorId = actor.Id,
                    EventType = "actor.move",
                    Message = $"{actor.Name} stalks closer.",
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ActorInstance?> TrySpawnActorInRoomAsync(RunState run, Guid roomId, string triggerEventType,
        CancellationToken cancellationToken)
    {
        var diff = await new DifficultyService(_db).GetOrDefaultAsync(run.DifficultyKey, cancellationToken);

        var room = await _db.RoomInstances.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == roomId, cancellationToken);

        var counts = await GetActorCountsInRoomAsync(run.RunId, roomId, cancellationToken);

        // If both are capped, no spawn.
        if (counts.Npcs >= Math.Max(0, diff.MaxNpcsPerRoom) && counts.Enemies >= Math.Max(0, diff.MaxEnemiesPerRoom))
            return null;

        var rng = new Random(HashCode.Combine(run.Seed, run.Turn, roomId.GetHashCode(), 443));
        var kind = ChooseActorKind(rng, diff);

        // Suppress new enemy spawns in cleared rooms.
        if (room is not null && room.IsCleared && kind == ActorKind.Enemy)
            kind = ActorKind.Npc;

        // Respect caps (fallback to other type if chosen type is capped)
        if (kind == ActorKind.Npc && counts.Npcs >= Math.Max(0, diff.MaxNpcsPerRoom))
            kind = ActorKind.Enemy;
        if (kind == ActorKind.Enemy && counts.Enemies >= Math.Max(0, diff.MaxEnemiesPerRoom))
            kind = ActorKind.Npc;

        // Re-apply cleared-room gate after fallback.
        if (room is not null && room.IsCleared && kind == ActorKind.Enemy)
            return null;

        if (kind == ActorKind.Npc && counts.Npcs >= Math.Max(0, diff.MaxNpcsPerRoom))
            return null;
        if (kind == ActorKind.Enemy && counts.Enemies >= Math.Max(0, diff.MaxEnemiesPerRoom))
            return null;

        var actor = kind == ActorKind.Npc
            ? CreateNpc(run.RunId, roomId, rng)
            : CreateEnemy(run.RunId, roomId, rng);

        _db.ActorInstances.Add(actor);

        var state = new RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actor.Id,
            CurrentNodeKey = "encounter.stranger.1",
            ConversationPhase = "opening",
            UpdatedUtc = DateTime.UtcNow
        };
        _db.RunDialogueStates.Add(state);

        _db.RoomEventLogs.Add(new RoomEventLog
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Turn = run.Turn,
            ActorId = actor.Id,
            EventType = triggerEventType,
            Message = $"An unknown presence stirs here: {actor.Name}.",
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        await TryComposeProceduralDialogueAsync(run, actor, state, cancellationToken);
        return actor;
    }

    private async Task TryComposeProceduralDialogueAsync(RunState run, ActorInstance actor, RunDialogueState state,
        CancellationToken cancellationToken)
    {
        try
        {
            var cfg = await _db.RunConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.RunId == run.RunId, cancellationToken);
            var enabledPacks = (IReadOnlyList<string>)(cfg?.EnabledLorePacks ?? new List<string>);

            var ctxRoom = await GetCurrentRoomAsync(run, cancellationToken);
            var contextTags = new List<string> { "encounter" };

            // Add actor-kind tags so snippets can target NPC/enemy differently.
            if (actor.Kind == ActorKind.Npc)
            {
                contextTags.Add("npc");
                if (actor.AutoSpeakOnEnter) contextTags.Add("auto");
            }
            else
            {
                contextTags.Add("enemy");
                contextTags.Add(actor.SpeechLevel >= 2 ? "advanced" : "simple");
                if (!actor.PacifyUnlocked) contextTags.Add("pacify");
            }

            if (ctxRoom?.RoomTags is not null) contextTags.AddRange(ctxRoom.RoomTags);

            var currentPhase = string.IsNullOrWhiteSpace(state.ConversationPhase) ? "opening" : state.ConversationPhase;

            var composer = new ProceduralDialogueComposer(_db);
            var composed = await composer.ComposeAsync(new ProceduralDialogueComposer.ComposeRequest(
                RunId: run.RunId,
                Seed: run.Seed,
                Turn: run.Turn,
                PlayerName: run.PlayerName,
                RoomName: ctxRoom?.Name ?? "",
                EnabledLorePacks: enabledPacks,
                ContextTags: contextTags,
                Sanity: run.Sanity,
                Morality: run.Morality,
                Disposition: actor.Disposition,
                MaxLines: 1,
                SeedOffset: cfg?.DialogueSeedOffset,
                Phase: currentPhase
            ), cancellationToken);

            if (string.IsNullOrWhiteSpace(composed.Text))
                return;

            state.LastComposedSnippetKeys = string.Join(';', composed.SnippetKeys);
            state.ConversationPhase = currentPhase.Equals("opening", StringComparison.OrdinalIgnoreCase)
                ? "middle"
                : currentPhase.Equals("middle", StringComparison.OrdinalIgnoreCase)
                    ? "closing"
                    : "closing";

            state.UpdatedUtc = DateTime.UtcNow;
            _db.RunDialogueStates.Update(state);

            _db.RoomEventLogs.Add(new RoomEventLog
            {
                Id = Guid.NewGuid(),
                RunId = run.RunId,
                ActorId = actor.Id,
                Turn = run.Turn,
                EventType = "dialogue.procedural",
                Message = composed.Text,
                CreatedUtc = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Never block gameplay on procedural dialogue.
        }
    }
}
