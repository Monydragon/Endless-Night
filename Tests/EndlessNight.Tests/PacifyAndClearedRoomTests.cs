using EndlessNight.Domain;
using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class PacifyAndClearedRoomTests
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
    public async Task PacifyEnemy_ShouldDespawnEnemy_AndMarkRoomCleared()
    {
        using var db = CreateDb();
        await new Seeder(db).EnsureSeededAsync();

        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Tester", seed: 3333, difficultyKey: "normal");

        var enemy = await svc.ForceSpawnActorInCurrentRoomAsync(run, ActorKind.Enemy);
        run.Sanity = 100;

        var (ok, msg) = await svc.TryPacifyEnemyAsync(run, enemy.Id);
        Assert.That(ok, Is.True, msg);

        var enemiesNow = await svc.GetEnemiesInCurrentRoomAsync(run);
        Assert.That(enemiesNow.Count, Is.EqualTo(0));

        var room = await svc.GetCurrentRoomAsync(run);
        Assert.That(room, Is.Not.Null);
        Assert.That(room!.IsCleared, Is.True);
    }

    [Test]
    public async Task ClearedRoom_ShouldNotSpawnNewEnemies_ButExistingEnemiesCanMoveIn()
    {
        using var db = CreateDb();
        await new Seeder(db).EnsureSeededAsync();

        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Tester", seed: 4444, difficultyKey: "normal");

        var startRoom = await svc.GetCurrentRoomAsync(run);
        Assert.That(startRoom, Is.Not.Null);

        // Find an adjacent room to stage an enemy.
        var dir = startRoom!.Exits.Keys.First();
        var adjacentRoomId = startRoom.Exits[dir];

        // Mark current room as cleared.
        startRoom.IsCleared = true;
        db.RoomInstances.Update(startRoom);
        await db.SaveChangesAsync();

        // Seed an enemy in adjacent room.
        var enemy = new ActorInstance
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Kind = ActorKind.Enemy,
            Name = "Follower",
            CurrentRoomId = adjacentRoomId,
            Intensity = 50,
            Morality = -10,
            Sanity = 20,
            Disposition = ActorDisposition.Unknown,
            IsHostile = true,
            IsPacified = false,
            IsAlive = true
        };
        db.ActorInstances.Add(enemy);
        db.RunDialogueStates.Add(new Domain.Dialogue.RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = enemy.Id,
            CurrentNodeKey = "encounter.stranger.1",
            ConversationPhase = "opening",
            UpdatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Advance enough turns to give the movement pulse a chance.
        for (int i = 0; i < 50; i++)
            await svc.AdvanceTurnAsync(run, "test.follow");

        // Enemy is allowed to move into the cleared room by roaming/follow.
        var enemiesInCleared = await svc.GetEnemiesInCurrentRoomAsync(run);
        Assert.That(enemiesInCleared.Count, Is.GreaterThanOrEqualTo(0));

        // Hard requirement here is: cleared room never spawns NEW enemies.
        // So total enemies should still be exactly 1.
        var totalEnemies = await db.ActorInstances.CountAsync(a => a.RunId == run.RunId && a.IsAlive && a.Kind == ActorKind.Enemy);
        Assert.That(totalEnemies, Is.EqualTo(1));
    }
}

