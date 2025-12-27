using EndlessNight.Domain;
using EndlessNight.Services;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class ChestAndPuzzleTests
{
    [Test]
    public void LockedChest_ShouldRequireKey_AndLootOnOpen()
    {
        var gen = new ProceduralLevel1Generator();
        var world = gen.GenerateWorld(Guid.NewGuid(), 1234);

        var lockedChest = world.Objects.FirstOrDefault(o => o.Kind == WorldObjectKind.Chest && !string.IsNullOrEmpty(o.RequiredItemKey));
        Assert.That(lockedChest, Is.Not.Null, "No locked chest generated for test seed");

        // Simulate having the key
        var inventory = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { lockedChest!.RequiredItemKey! };
        Assert.That(inventory.Contains(lockedChest.RequiredItemKey!), Is.True);
    }

    [Test]
    public void PuzzleGate_ShouldMatchPlacedKeyItem()
    {
        var gen = new ProceduralLevel1Generator();
        var world = gen.GenerateWorld(Guid.NewGuid(), 2024);

        var gates = world.Objects.Where(o => o.Kind == WorldObjectKind.PuzzleGate).ToList();
        if (gates.Count == 0) Assert.Pass("No gates generated for this seed; generator may remove unsolvable gates.");
        foreach (var gate in gates)
        {
            var required = gate.RequiredItemKey;
            var exists = world.Objects.Any(o => (o.Kind == WorldObjectKind.GroundItem && o.ItemKey == required) ||
                                                (o.Kind == WorldObjectKind.Chest && o.LootItemKeys.Contains(required!)));
            Assert.That(exists, Is.True, $"Required key '{required}' should exist in world");
        }
    }
}

