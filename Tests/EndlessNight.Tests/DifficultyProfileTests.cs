using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EndlessNight.Tests;

[TestFixture]
public sealed class DifficultyProfileTests
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

        // Seed profiles
        new Seeder(db).EnsureSeededAsync().GetAwaiter().GetResult();

        return db;
    }

    [Test]
    public void Seeder_ShouldCreate_DefaultDifficultyProfiles()
    {
        using var db = CreateDbAndSeed();
        Assert.That(db.DifficultyProfiles.Count(), Is.GreaterThanOrEqualTo(7));
        Assert.That(db.DifficultyProfiles.Any(p => p.Key == "normal"), Is.True);
        Assert.That(db.DifficultyProfiles.Any(p => p.Key == "endless" && p.IsEndless), Is.True);
    }

    [Test]
    public async Task RunService_CreateNewRun_ShouldPersistDifficultyKey_AndApplyStartingStats()
    {
        using var db = CreateDbAndSeed();

        var svc = new RunService(db, new ProceduralLevel1Generator());
        var run = await svc.CreateNewRunAsync("Tester", seed: 42, difficultyKey: "very-hard");

        Assert.That(run.DifficultyKey, Is.EqualTo("very-hard"));
        Assert.That(run.Health, Is.EqualTo(85));
        Assert.That(run.Sanity, Is.EqualTo(85));

        var persisted = await svc.GetRunAsync(run.RunId);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.DifficultyKey, Is.EqualTo("very-hard"));
    }
}

