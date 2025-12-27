using EndlessNight.Domain;
using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class DialogueTalkFlowTests
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
    public async Task TalkFlow_SpawnNpc_GetDialogue_ChooseChoice_ShouldApplyRunDeltas()
    {
        using var db = CreateDb();
        await new Seeder(db).EnsureSeededAsync();

        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Tester", seed: 2222, difficultyKey: "casual");

        // Ensure an NPC with a dialogue state exists in the room.
        var actor = await svc.ForceSpawnActorInCurrentRoomAsync(run, ActorKind.Npc);

        var state = await db.RunDialogueStates.FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actor.Id);
        Assert.That(state, Is.Not.Null, "ForceSpawn should attach a dialogue state now");

        var beforeHealth = run.Health;
        var beforeSanity = run.Sanity;
        var beforeMorality = run.Morality;

        var (node, choices, err) = await svc.GetDialogueAsync(run, actor.Id);
        Assert.That(err, Is.Null);
        Assert.That(node, Is.Not.Null);
        Assert.That(choices, Is.Not.Empty);

        var chosen = choices[0];
        var (ok, msg) = await svc.ChooseDialogueAsync(run, actor.Id, chosen);
        Assert.That(ok, Is.True, msg);

        // Deltas should be applied exactly.
        Assert.That(run.Health, Is.EqualTo(Math.Clamp(beforeHealth + chosen.HealthDelta, 0, 100)));
        Assert.That(run.Sanity, Is.EqualTo(Math.Clamp(beforeSanity + chosen.SanityDelta, 0, 100)));
        Assert.That(run.Morality, Is.EqualTo(Math.Clamp(beforeMorality + chosen.MoralityDelta, -100, 100)));
    }
}
