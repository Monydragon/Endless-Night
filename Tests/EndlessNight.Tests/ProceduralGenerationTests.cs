using EndlessNight.Domain;
using EndlessNight.Services;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class ProceduralGenerationTests
{
    [Test]
    public void GenerateWorld_ShouldCreateRooms_AndObjects()
    {
        var gen = new ProceduralLevel1Generator();
        var runId = Guid.NewGuid();
        var world = gen.GenerateWorld(runId, 12345);

        Assert.That(world.Rooms.Count, Is.GreaterThanOrEqualTo(7));
        Assert.That(world.Objects.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Rooms_ShouldHaveCoordinatesAndNames()
    {
        var gen = new ProceduralLevel1Generator();
        var world = gen.GenerateWorld(Guid.NewGuid(), 42);

        foreach (var room in world.Rooms)
        {
            Assert.That(room.Name, Is.Not.Null.And.Not.Empty);
            _ = room.X;
            _ = room.Y;
        }
    }

    [Test]
    public void World_ShouldContainCampfires()
    {
        var gen = new ProceduralLevel1Generator();
        var world = gen.GenerateWorld(Guid.NewGuid(), 99);

        var campfires = world.Objects.Where(o => o.Kind == WorldObjectKind.Campfire).ToList();
        Assert.That(campfires.Count, Is.InRange(2, 3));
    }
}
