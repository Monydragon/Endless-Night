using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class ProceduralDialogueComposerTests
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
    public async Task ConstraintFiltering_SanityMax_ShouldExcludeSnippet_WhenSanityTooHigh()
    {
        using var db = CreateDbAndSeed();
        var composer = new ProceduralDialogueComposer(db);

        // This should exclude lovecraft.middle.lowSanity.001 because MaxSanity=30.
        var req = new ProceduralDialogueComposer.ComposeRequest(
            RunId: Guid.NewGuid(),
            Seed: 123,
            Turn: 1,
            PlayerName: "Tester",
            RoomName: "Foyer",
            EnabledLorePacks: new[] { "lovecraft" },
            ContextTags: new[] { "encounter", "cosmic" },
            Sanity: 95,
            Morality: 0,
            Disposition: EndlessNight.Domain.ActorDisposition.Unknown,
            MaxLines: 3,
            SeedOffset: 0);

        var result = await composer.ComposeAsync(req);
        Assert.That(result.SnippetKeys.Contains("lovecraft.middle.lowSanity.001"), Is.False);
    }

    [Test]
    public async Task WeightedSelection_HigherWeightSnippet_ShouldAppearMoreOften_OverManyDeterministicTrials()
    {
        using var db = CreateDbAndSeed();

        // Add two controlled snippets with very different weights.
        db.DialogueSnippets.Add(new EndlessNight.Domain.Dialogue.DialogueSnippet
        {
            Id = Guid.NewGuid(),
            Key = "test.weight.low",
            Text = "LOW",
            Tags = "encounter",
            Weight = 1,
            Role = "opening"
        });
        db.DialogueSnippets.Add(new EndlessNight.Domain.Dialogue.DialogueSnippet
        {
            Id = Guid.NewGuid(),
            Key = "test.weight.high",
            Text = "HIGH",
            Tags = "encounter",
            Weight = 20,
            Role = "opening"
        });
        await db.SaveChangesAsync();

        var composer = new ProceduralDialogueComposer(db);

        var high = 0;
        var low = 0;

        // "Deterministic trials": vary SeedOffset (or turn) in a stable loop.
        for (var i = 0; i < 200; i++)
        {
            var req = new ProceduralDialogueComposer.ComposeRequest(
                RunId: Guid.Empty,
                Seed: 9001,
                Turn: i,
                PlayerName: "Tester",
                RoomName: "Room",
                EnabledLorePacks: Array.Empty<string>(),
                ContextTags: new[] { "encounter" },
                Sanity: 50,
                Morality: 0,
                Disposition: EndlessNight.Domain.ActorDisposition.Unknown,
                MaxLines: 1,
                SeedOffset: 0);

            var result = await composer.ComposeAsync(req);
            if (result.SnippetKeys.Contains("test.weight.high")) high++;
            if (result.SnippetKeys.Contains("test.weight.low")) low++;
        }

        // Wide threshold: high should dominate.
        Assert.That(high, Is.GreaterThan(low * 3));
    }
}

