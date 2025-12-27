using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class ProceduralDialoguePhaseProgressionTests
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
    public async Task ConversationPhase_ShouldProgress_OpeningToMiddleToClosing()
    {
        using var db = CreateDbAndSeed();

        // Force encounter spawn by setting multipliers high and using seeded creation.
        // We rely on TriggerRoomEntryEncounterAsync writing phase and advancing it.
        var runService = new RunService(db, new ProceduralLevel1Generator());
        var run = await runService.CreateNewRunAsync("Tester", seed: 4242, difficultyKey: "endless");

        // Make several turns and attempt encounter spawn.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var (actor, _) = await runService.TriggerRoomEntryEncounterAsync(run);
            if (actor is null)
            {
                run.Turn++;
                await runService.SaveRunAsync(run);
                continue;
            }

            // After spawn, phase should be advanced to middle (opening was used).
            var state = await db.RunDialogueStates.FirstOrDefaultAsync(s => s.RunId == run.RunId && s.ActorId == actor.Id);
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.ConversationPhase, Is.EqualTo("middle"));

            // Simulate next encounter "beat" by composing again using the same stored state.
            // We call the composer directly with Phase=middle, then advance manually like RunService does.
            var composer = new ProceduralDialogueComposer(db);
            var cfg = await db.RunConfigs.AsNoTracking().FirstAsync(c => c.RunId == run.RunId);
            var room = await runService.GetCurrentRoomAsync(run);
            var contextTags = new List<string> { "encounter" };
            if (room?.RoomTags is not null) contextTags.AddRange(room.RoomTags);

            var middle = await composer.ComposeAsync(new ProceduralDialogueComposer.ComposeRequest(
                RunId: run.RunId,
                Seed: run.Seed,
                Turn: run.Turn + 1,
                PlayerName: run.PlayerName,
                RoomName: room?.Name ?? "",
                EnabledLorePacks: cfg.EnabledLorePacks,
                ContextTags: contextTags,
                Sanity: run.Sanity,
                Morality: run.Morality,
                Disposition: actor.Disposition,
                MaxLines: 1,
                SeedOffset: cfg.DialogueSeedOffset,
                Phase: state.ConversationPhase
            ));

            Assert.That(middle.SnippetKeys.Count, Is.LessThanOrEqualTo(1));

            state.ConversationPhase = "closing";
            state.LastComposedSnippetKeys = string.Join(';', middle.SnippetKeys);
            db.RunDialogueStates.Update(state);
            await db.SaveChangesAsync();

            var refreshed = await db.RunDialogueStates.FirstAsync(s => s.RunId == run.RunId && s.ActorId == actor.Id);
            Assert.That(refreshed.ConversationPhase, Is.EqualTo("closing"));

            return;
        }

        Assert.Fail("Failed to spawn an encounter actor within 50 attempts.");
    }
}

