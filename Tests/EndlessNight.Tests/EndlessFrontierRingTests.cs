using EndlessNight.Domain;
using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class EndlessFrontierRingTests
{
    private SqliteDbContext CreateDbAndSeed()
    {
        var options = new DbContextOptionsBuilder<SqliteDbContext>()
            .UseSqlite("Data Source=:memory:")
            .EnableSensitiveDataLogging(false)
            .Options;

        var db = new SqliteDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        new Seeder(db).EnsureSeededAsync().GetAwaiter().GetResult();
        return db;
    }

    [Test]
    public async Task EndlessRun_ShouldHaveAdjacentRoomsPreGenerated()
    {
        using var db = CreateDbAndSeed();
        var svc = new RunService(db, new ProceduralLevel1Generator());

        var run = await svc.CreateNewRunAsync("Endless", seed: 1337, difficultyKey: "endless");
        var start = await svc.GetCurrentRoomAsync(run);
        Assert.That(start, Is.Not.Null);

        var cfg = await db.RunConfigs.FirstAsync(c => c.RunId == run.RunId);
        Assert.That(start!.Exits.Count, Is.GreaterThanOrEqualTo(cfg.MinNewExitsPerRoom));

        // And all linked rooms should exist.
        foreach (var exit in start.Exits)
        {
            var next = await db.RoomInstances.FirstOrDefaultAsync(r => r.RunId == run.RunId && r.RoomId == exit.Value);
            Assert.That(next, Is.Not.Null, $"Missing room for exit {exit.Key}");
        }
    }

    [Test]
    public async Task FrontierExpansion_ShouldBeDeterministic_ForSameSeedAndMoves()
    {
        // Create two runs with same seed. After one move, the room graph around the player should match in count.
        using var db1 = CreateDbAndSeed();
        using var db2 = CreateDbAndSeed();

        var svc1 = new RunService(db1, new ProceduralLevel1Generator());
        var svc2 = new RunService(db2, new ProceduralLevel1Generator());

        var r1 = await svc1.CreateNewRunAsync("A", seed: 9001, difficultyKey: "endless");
        var r2 = await svc2.CreateNewRunAsync("B", seed: 9001, difficultyKey: "endless");

        var start1 = await svc1.GetCurrentRoomAsync(r1);
        var start2 = await svc2.GetCurrentRoomAsync(r2);
        Assert.That(start1, Is.Not.Null);
        Assert.That(start2, Is.Not.Null);

        // Move north in both.
        var (ok1, err1) = await svc1.MoveAsync(r1, Direction.North);
        var (ok2, err2) = await svc2.MoveAsync(r2, Direction.North);
        Assert.That(ok1, Is.True, err1);
        Assert.That(ok2, Is.True, err2);

        // Compare total room counts after identical path.
        var count1 = await db1.RoomInstances.CountAsync(x => x.RunId == r1.RunId);
        var count2 = await db2.RoomInstances.CountAsync(x => x.RunId == r2.RunId);
        Assert.That(count1, Is.EqualTo(count2));

        // Compare cursor advanced equally.
        var cfg1 = await db1.RunConfigs.FirstAsync(c => c.RunId == r1.RunId);
        var cfg2 = await db2.RunConfigs.FirstAsync(c => c.RunId == r2.RunId);
        Assert.That(cfg1.WorldGenCursor, Is.EqualTo(cfg2.WorldGenCursor));
    }
}
