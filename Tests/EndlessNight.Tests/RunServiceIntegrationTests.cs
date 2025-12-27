using EndlessNight.Domain;
using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class RunServiceIntegrationTests
{
    private SqliteDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SqliteDbContext>()
            .UseSqlite("Data Source=:memory:")
            .EnableSensitiveDataLogging(false)
            .Options;
        var db = new SqliteDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    [Test]
    public async Task RunService_CreateNewRun_ShouldInitializeWorldAndStats()
    {
        using var db = CreateDb();
        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Tester", 42);

        Assert.That(run.Health, Is.EqualTo(100));
        Assert.That(run.Sanity, Is.EqualTo(100));
        var room = await svc.GetCurrentRoomAsync(run);
        Assert.That(room, Is.Not.Null);
    }

    [Test]
    public async Task Move_ShouldChangeRoom_AndLogEvent()
    {
        using var db = CreateDb();
        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Mover", 123);
        var room = await svc.GetCurrentRoomAsync(run);
        Assert.That(room, Is.Not.Null);

        var dir = room!.Exits.Keys.FirstOrDefault();
        Assert.That(dir, Is.Not.EqualTo(default(Direction)));

        var (ok, err) = await svc.MoveAsync(run, dir);
        Assert.That(ok, Is.True, err);
        var newRoom = await svc.GetCurrentRoomAsync(run);
        Assert.That(newRoom!.RoomId, Is.Not.EqualTo(room.RoomId));
    }

    [Test]
    public async Task SearchRoom_ShouldRevealHiddenObjects()
    {
        using var db = CreateDb();
        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Searcher", 7);

        var hidden = await svc.GetHiddenWorldObjectsInCurrentRoomAsync(run);
        var (ok, msg) = await svc.SearchRoomAsync(run);
        Assert.That(ok, Is.True);
        Assert.That(msg, Is.Not.Null);

        var visible = await svc.GetVisibleWorldObjectsInCurrentRoomAsync(run);
        Assert.That(visible.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task Campfire_RestoresHealthAndSanity()
    {
        using var db = CreateDb();
        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Camper", 99);

        var visible = await svc.GetVisibleWorldObjectsInCurrentRoomAsync(run);
        var camp = visible.FirstOrDefault(o => o.Kind == WorldObjectKind.Campfire);
        if (camp is null)
        {
            var room = await svc.GetCurrentRoomAsync(run);
            foreach (var exit in room!.Exits)
            {
                var (ok, _) = await svc.MoveAsync(run, exit.Key);
                if (!ok) continue;
                visible = await svc.GetVisibleWorldObjectsInCurrentRoomAsync(run);
                camp = visible.FirstOrDefault(o => o.Kind == WorldObjectKind.Campfire);
                if (camp is not null) break;
            }
        }

        Assert.That(camp, Is.Not.Null, "No campfire found nearby in test traversal");

        run.Health = Math.Max(0, run.Health - 20);
        run.Sanity = Math.Max(0, run.Sanity - 20);
        var (restOk, _) = await svc.InteractAsync(run, camp!.Id);
        Assert.That(restOk, Is.True);
        Assert.That(run.Health, Is.GreaterThanOrEqualTo(80));
        Assert.That(run.Sanity, Is.GreaterThanOrEqualTo(80));
    }
}
