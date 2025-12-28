using EndlessNight.Domain;
using EndlessNight.Services.Feedback;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class FeedbackSoundPatternTests
{
    [Test]
    public void MenuMove_PitchShouldDrop_WhenSanityIsLow()
    {
        var stable = new RunState { PlayerName = "t", RunId = Guid.NewGuid(), Seed = 1, CurrentRoomId = Guid.NewGuid(), Health = 100, Sanity = 90 };
        var broken = new RunState { PlayerName = "t", RunId = stable.RunId, Seed = 1, CurrentRoomId = stable.CurrentRoomId, Health = 100, Sanity = 5 };

        var stablePattern = GetPattern(stable, "MenuMove");
        var brokenPattern = GetPattern(broken, "MenuMove");

        Assert.That(brokenPattern.f1, Is.LessThan(stablePattern.f1));
    }

    [Test]
    public void MenuMove_PitchShouldDrop_WhenHealthIsCritical()
    {
        var safe = new RunState { PlayerName = "t", RunId = Guid.NewGuid(), Seed = 2, CurrentRoomId = Guid.NewGuid(), Health = 90, Sanity = 70 };
        var critical = new RunState { PlayerName = "t", RunId = safe.RunId, Seed = 2, CurrentRoomId = safe.CurrentRoomId, Health = 10, Sanity = 70 };

        var safePattern = GetPattern(safe, "MenuMove");
        var criticalPattern = GetPattern(critical, "MenuMove");

        Assert.That(criticalPattern.f1, Is.LessThan(safePattern.f1));
    }

    private static (int f1, int d1, int f2, int d2) GetPattern(RunState run, string kindName)
    {
        var enumType = typeof(FeedbackService).GetNestedType("FeedbackSoundKind", System.Reflection.BindingFlags.NonPublic);
        Assert.That(enumType, Is.Not.Null);

        var kind = Enum.Parse(enumType!, kindName);
        var method = typeof(FeedbackService).GetMethod(
            "GetBeepPattern",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        // Returns a ValueTuple<int,int,int,int>
        var result = method!.Invoke(null, new object?[] { run, kind });
        Assert.That(result, Is.Not.Null);
        return ((int f1, int d1, int f2, int d2))result!;
    }
}
