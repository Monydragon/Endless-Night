using EndlessNight.Services;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class PuzzleSolvabilityValidatorTests
{
    [Test]
    public void GeneratedWorld_ShouldNotContainUnsolvableGates_ForSeveralSeeds()
    {
        var gen = new ProceduralLevel1Generator();

        var seeds = new[] { 1, 2, 3, 42, 12345, -7, int.MinValue + 1, int.MaxValue - 1 };

        foreach (var seed in seeds)
        {
            var runId = Guid.NewGuid();
            var world = gen.GenerateWorld(runId, seed);
            var startRoomId = world.Rooms[0].RoomId;

            Assert.That(
                PuzzleSolvabilityValidator.AreAllGatesSolvable(world.Rooms, world.Objects, startRoomId),
                Is.True,
                $"Gate solvability failed for seed {seed}"
            );
        }
    }
}

