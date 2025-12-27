using EndlessNight.Domain;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

/// <summary>
/// Ensures a frontier ring of rooms exists around the player (generate-ahead exploration).
/// This supports endless runs by creating new rooms deterministically and persisting them.
/// </summary>
public sealed class FrontierExpansionService
{
    private readonly SqliteDbContext _db;
    private readonly EndlessRoomGenerator _generator;

    public FrontierExpansionService(SqliteDbContext db, EndlessRoomGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    public async Task EnsureFrontierRingAsync(RunState run, CancellationToken cancellationToken = default)
    {
        var cfg = await _db.RunConfigs.FirstOrDefaultAsync(c => c.RunId == run.RunId, cancellationToken);
        if (cfg is null || !cfg.EndlessEnabled)
            return;

        var radius = Math.Max(0, cfg.FrontierRingRadius);
        if (radius == 0)
            return;

        var start = await _db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == run.CurrentRoomId, cancellationToken);
        if (start is null)
            return;

        // Deterministic RNG for expansion decisions. Cursor and turn affect topology but are stable for the same play path.
        var rng = new Random(HashCode.Combine(run.Seed, cfg.WorldGenCursor, run.Turn, run.CurrentRoomId.GetHashCode()));

        var roomIndex = await LoadRoomIndexAsync(run, cancellationToken);

        var generatedCount = 0;
        var queue = new Queue<(RoomInstance room, int dist)>();
        var visited = new HashSet<Guid>();
        queue.Enqueue((start, 0));
        visited.Add(start.RoomId);

        while (queue.Count > 0)
        {
            var (room, dist) = queue.Dequeue();
            if (dist >= radius)
                continue;

            // Decide which directions to expand from this room.
            // We always allow connecting to existing rooms if it reduces dead ends.
            var candidates = Enum.GetValues<Direction>().ToList();

            // Randomize direction order deterministically.
            candidates = candidates.OrderBy(_ => rng.Next()).ToList();

            // How many new exits we want to ensure for this room.
            var minExits = Math.Clamp(cfg.MinNewExitsPerRoom, 0, 4);
            var maxExits = Math.Clamp(cfg.MaxNewExitsPerRoom, minExits, 4);
            var targetNewExits = rng.Next(minExits, maxExits + 1);

            // Prefer not to exceed target exits when room already has many exits.
            var missing = candidates.Where(d => !room.Exits.ContainsKey(d)).ToList();
            var toCreate = missing.Take(Math.Max(0, targetNewExits)).ToList();

            foreach (var dir in toCreate)
            {
                if (generatedCount >= cfg.FrontierMaxRoomsPerTurn)
                    break;

                // If already connected meanwhile, skip.
                if (room.Exits.ContainsKey(dir))
                    continue;

                var (nx, ny) = Step(room.X, room.Y, dir);

                // If a room already exists at target coords, connect to it.
                if (roomIndex.TryGetValue((nx, ny), out var existingAtCoords))
                {
                    room.Exits[dir] = existingAtCoords.RoomId;
                    existingAtCoords.Exits[Opposite(dir)] = room.RoomId;

                    // Track for BFS.
                    if (visited.Add(existingAtCoords.RoomId))
                        queue.Enqueue((existingAtCoords, dist + 1));

                    continue;
                }

                // Otherwise generate a new room.
                var dangerBase = Math.Clamp(room.DangerRating + (dist >= 1 ? 1 : 0), 0, 5);

                // Difficulty influences scaling.
                var difficulty = await _db.DifficultyProfiles.FirstOrDefaultAsync(d => d.Key == run.DifficultyKey, cancellationToken);
                var dangerScale = difficulty?.EnemySpawnMultiplier ?? 1.0f;

                var gen = _generator.GenerateRoom(
                    run.RunId,
                    run.Seed,
                    cfg.WorldGenCursor,
                    nx,
                    ny,
                    parentDepth: room.Depth,
                    baseDanger: dangerBase,
                    dangerScale: dangerScale,
                    enabledLorePacks: cfg.EnabledLorePacks
                );
                cfg.WorldGenCursor++;
                cfg.UpdatedUtc = DateTime.UtcNow;

                gen.Room.Id = Guid.NewGuid();
                await _db.RoomInstances.AddAsync(gen.Room, cancellationToken);

                foreach (var obj in gen.Objects)
                    await _db.WorldObjects.AddAsync(obj, cancellationToken);

                // Connect bidirectionally.
                room.Exits[dir] = gen.Room.RoomId;
                gen.Room.Exits[Opposite(dir)] = room.RoomId;

                // Update index.
                roomIndex[(nx, ny)] = gen.Room;

                generatedCount++;

                queue.Enqueue((gen.Room, dist + 1));
                visited.Add(gen.Room.RoomId);

                if (cfg.MaxRooms is not null)
                {
                    var currentCount = roomIndex.Count;
                    if (currentCount >= cfg.MaxRooms.Value)
                        break;
                }
            }

            if (generatedCount >= cfg.FrontierMaxRoomsPerTurn)
                break;
        }

        _db.RunConfigs.Update(cfg);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<(int x, int y), RoomInstance>> LoadRoomIndexAsync(RunState run, CancellationToken cancellationToken)
    {
        var rooms = await _db.RoomInstances
            .Where(r => r.RunId == run.RunId)
            .ToListAsync(cancellationToken);

        // Last-writer-wins if coords collide (should be rare; later we can enforce uniqueness at DB level).
        var dict = new Dictionary<(int x, int y), RoomInstance>();
        foreach (var r in rooms)
            dict[(r.X, r.Y)] = r;

        return dict;
    }

    private static (int x, int y) Step(int x, int y, Direction dir)
        => dir switch
        {
            Direction.North => (x, y + 1),
            Direction.South => (x, y - 1),
            Direction.East => (x + 1, y),
            Direction.West => (x - 1, y),
            _ => (x, y)
        };

    private static Direction Opposite(Direction dir)
        => dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => dir
        };
}
