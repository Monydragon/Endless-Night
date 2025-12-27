using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class FearWordSelectionTests
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
    public async Task FearWord_ShouldNotUsePackWords_WhenPackDisabled()
    {
        using var db = CreateDbAndSeed();
        var composer = new ProceduralDialogueComposer(db);

        // Ensure output includes a fear word token.
        db.DialogueSnippets.Add(new EndlessNight.Domain.Dialogue.DialogueSnippet
        {
            Id = Guid.NewGuid(),
            Key = "test.fearword.001",
            Role = "opening",
            Tags = "__fearword_test__",
            Weight = 1,
            Text = "You hear {fearWord}."
        });
        await db.SaveChangesAsync();

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ichor", "eldritch", "non-Euclid", "grue" };

        for (var i = 0; i < 50; i++)
        {
            var res = await composer.ComposeAsync(new ProceduralDialogueComposer.ComposeRequest(
                RunId: Guid.Empty,
                Seed: 1234,
                Turn: i,
                PlayerName: "Tester",
                RoomName: "Room",
                EnabledLorePacks: Array.Empty<string>(),
                ContextTags: new[] { "__fearword_test__" },
                Sanity: 10,
                Morality: 0,
                Disposition: EndlessNight.Domain.ActorDisposition.Unknown,
                MaxLines: 1,
                SeedOffset: 0,
                Phase: "opening"
            ));

            foreach (var bad in forbidden)
                Assert.That(res.Text, Does.Not.Contain(bad));
        }
    }

    [Test]
    public async Task FearWord_LowSanity_ShouldFavorPackSpecificWords_WhenPackEnabled()
    {
        using var db = CreateDbAndSeed();
        var composer = new ProceduralDialogueComposer(db);

        // Ensure output includes a fear word token.
        db.DialogueSnippets.Add(new EndlessNight.Domain.Dialogue.DialogueSnippet
        {
            Id = Guid.NewGuid(),
            Key = "test.fearword.002",
            Role = "opening",
            Tags = "__fearword_test__",
            Weight = 1,
            Text = "You hear {fearWord}."
        });
        await db.SaveChangesAsync();

        var lovecraftWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ichor", "eldritch", "non-Euclid" };

        var packHits = 0;
        var trials = 200;

        for (var i = 0; i < trials; i++)
        {
            var res = await composer.ComposeAsync(new ProceduralDialogueComposer.ComposeRequest(
                RunId: Guid.Empty,
                Seed: 9001,
                Turn: i,
                PlayerName: "Tester",
                RoomName: "Room",
                EnabledLorePacks: new[] { "lovecraft" },
                ContextTags: new[] { "__fearword_test__" },
                Sanity: 0,
                Morality: 0,
                Disposition: EndlessNight.Domain.ActorDisposition.Unknown,
                MaxLines: 1,
                SeedOffset: 0,
                Phase: "opening"
            ));

            if (lovecraftWords.Any(w => res.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                packHits++;
        }

        // Wide threshold: at 0 sanity, pack words should appear fairly often.
        Assert.That(packHits, Is.GreaterThan(trials * 0.25));
    }
}

