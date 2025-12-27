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

        var generatedCount = 0;
        var radius = Math.Max(0, cfg.FrontierRingRadius);
        if (radius == 0)
            return;

        var start = await _db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == run.CurrentRoomId, cancellationToken);
        if (start is null)
            return;

        var queue = new Queue<(RoomInstance room, int dist)>();
        var visited = new HashSet<Guid>();
        queue.Enqueue((start, 0));
        visited.Add(start.RoomId);

        while (queue.Count > 0)
        {
            var (room, dist) = queue.Dequeue();
            if (dist >= radius)
                continue;

            foreach (var dir in Enum.GetValues<Direction>())
            {
                if (generatedCount >= cfg.FrontierMaxRoomsPerTurn)
                    break;

                if (!room.Exits.TryGetValue(dir, out var nextRoomId) || nextRoomId == Guid.Empty)
                {
                    var (nx, ny) = Step(room.X, room.Y, dir);
                    var dangerBase = Math.Clamp(room.DangerRating + (dist >= 1 ? 1 : 0), 0, 5);

                    var gen = _generator.GenerateRoom(run.RunId, run.Seed, cfg.WorldGenCursor, nx, ny, dangerBase);
                    cfg.WorldGenCursor++;
                    cfg.UpdatedUtc = DateTime.UtcNow;

                    // Persist room + objects (keep them tracked)
                    gen.Room.Id = Guid.NewGuid();
                    await _db.RoomInstances.AddAsync(gen.Room, cancellationToken);

                    foreach (var obj in gen.Objects)
                        await _db.WorldObjects.AddAsync(obj, cancellationToken);

                    // Connect bidirectionally.
                    room.Exits[dir] = gen.Room.RoomId;
                    gen.Room.Exits[Opposite(dir)] = room.RoomId;

                    generatedCount++;

                    queue.Enqueue((gen.Room, dist + 1));
                    visited.Add(gen.Room.RoomId);

                    // Optional cap: if configured, stop generating once the cap is met.
                    if (cfg.MaxRooms is not null)
                    {
                        // NOTE: CountAsync here is safe; we only do it when capped.
                        var currentCount = await _db.RoomInstances.CountAsync(r => r.RunId == run.RunId, cancellationToken);
                        if (currentCount >= cfg.MaxRooms.Value)
                            break;
                    }
                }
                else
                {
                    var next = await _db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == nextRoomId, cancellationToken);
                    if (next is not null && visited.Add(next.RoomId))
                        queue.Enqueue((next, dist + 1));
                }
            }

            if (generatedCount >= cfg.FrontierMaxRoomsPerTurn)
                break;
        }

        _db.RunConfigs.Update(cfg);
        await _db.SaveChangesAsync(cancellationToken);
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
