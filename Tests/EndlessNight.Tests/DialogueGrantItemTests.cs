using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class DialogueGrantItemTests
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
    public async Task DialogueChoice_WithGrantItem_ShouldAddToInventory()
    {
        using var db = CreateDbAndSeed();
        var svc = new RunService(db, new ProceduralLevel1Generator());

        var run = await svc.CreateNewRunAsync("Tester", seed: 2222, difficultyKey: "endless");

        // Pick a seeded choice that grants an item.
        var choice = await db.DialogueChoices
            .AsNoTracking()
            .Where(c => !string.IsNullOrWhiteSpace(c.GrantItemKey))
            .OrderBy(c => c.FromNodeKey)
            .ThenBy(c => c.Text)
            .FirstOrDefaultAsync();

        Assert.That(choice, Is.Not.Null, "Expected at least one seeded dialogue choice to grant an item.");

        // Create an actor + dialogue state pinned to that node.
        var actor = new EndlessNight.Domain.ActorInstance
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            CurrentRoomId = run.CurrentRoomId,
            Kind = EndlessNight.Domain.ActorKind.Npc,
            Name = "Tester NPC",
            Intensity = 10,
            Morality = 0,
            Sanity = 100,
            Disposition = EndlessNight.Domain.ActorDisposition.Unknown,
            IsHostile = false,
            IsPacified = false,
            IsAlive = true
        };
        db.ActorInstances.Add(actor);

        db.RunDialogueStates.Add(new EndlessNight.Domain.Dialogue.RunDialogueState
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            ActorId = actor.Id,
            CurrentNodeKey = choice!.FromNodeKey,
            UpdatedUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var (ok, msg) = await svc.ChooseDialogueAsync(run, actor.Id, choice);
        Assert.That(ok, Is.True, msg);

        var inv = await svc.GetInventoryAsync(run);
        var expectedQty = choice.GrantItemQuantity <= 0 ? 1 : choice.GrantItemQuantity;
        Assert.That(inv.Any(i => i.ItemKey == choice.GrantItemKey && i.Quantity >= expectedQty), Is.True);
    }
}
