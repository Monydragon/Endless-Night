using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
using EndlessNight.Domain.Story;
using EndlessNight.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Services;

public sealed class Seeder
{
    private readonly SqliteDbContext _db;

    public Seeder(SqliteDbContext db)
    {
        _db = db;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        await SeedLegacyMapAsync(cancellationToken);
        await SeedItemDefinitionsAsync(cancellationToken);
        await SeedDifficultyProfilesAsync(cancellationToken);
        await SeedDialogueAsync(cancellationToken);
        await SeedStoryChaptersAsync(cancellationToken);
        await SeedLorePacksAsync(cancellationToken);
        await SeedDialogueSnippetsAsync(cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedLegacyMapAsync(CancellationToken cancellationToken)
    {
        const string mapKey = "demo";

        var existingMap = await _db.Maps.FirstOrDefaultAsync(m => m.Key == mapKey, cancellationToken);
        if (existingMap is null)
        {
            _db.Maps.Add(new MapDefinition
            {
                Id = Guid.NewGuid(),
                Key = mapKey,
                Name = "Endless Night Demo",
                StartingRoomKey = "foyer"
            });
        }

        await UpsertRoomAsync(new Room
        {
            Id = Guid.NewGuid(),
            Key = "foyer",
            Name = "Foyer",
            Description = "A narrow foyer. The air is cold and tastes of dust.",
            Exits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["east"] = "library"
            }
        }, cancellationToken);

        await UpsertRoomAsync(new Room
        {
            Id = Guid.NewGuid(),
            Key = "library",
            Name = "Library",
            Description = "Shelves lean like tired men. Something scratches behind the books.",
            Exits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["west"] = "foyer"
            }
        }, cancellationToken);
    }

    private async Task SeedItemDefinitionsAsync(CancellationToken cancellationToken)
    {
        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "lantern",
            Name = "Lantern",
            Description = "A battered lantern. The flame is too steady.",
            Tags = new List<string> { "light", "tool" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "bandage",
            Name = "Bandage",
            Description = "Old cloth. Clean enough. Maybe.",
            Tags = new List<string> { "consumable", "sanity" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "health-potion",
            Name = "Crimson Tonic",
            Description = "A small vial that pulses with warmth. It smells like iron and roses.",
            Tags = new List<string> { "consumable", "health" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "note",
            Name = "Damp Note",
            Description = "Ink bleeds across the paper as if it remembers being alive.",
            Tags = new List<string> { "clue" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "rusty-key",
            Name = "Rusty Key",
            Description = "A key that tastes like pennies when you hold it.",
            Tags = new List<string> { "key" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "sigil",
            Name = "Chalk Sigil",
            Description = "A small artifact etched with a chalky sigil. It hums faintly when you hold it.",
            Tags = new List<string> { "key", "artifact" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "silver-key",
            Name = "Silver Key",
            Description = "An ornate silver key, cold to the touch. Ancient runes spiral along its shaft.",
            Tags = new List<string> { "key" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "ancient-tome",
            Name = "Ancient Tome",
            Description = "A leather-bound book. The pages whisper secrets in languages you shouldn't understand.",
            Tags = new List<string> { "artifact", "clue" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "crystal-shard",
            Name = "Crystal Shard",
            Description = "A shard of obsidian crystal that reflects light that isn't there.",
            Tags = new List<string> { "artifact", "key" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "torch",
            Name = "Torch",
            Description = "A wooden torch wrapped in oil-soaked cloth. The flame never wavers.",
            Tags = new List<string> { "light", "tool" }
        }, cancellationToken);

        await UpsertItemDefinitionAsync(new ItemDefinition
        {
            Id = Guid.NewGuid(),
            Key = "rope",
            Name = "Coiled Rope",
            Description = "Thick hemp rope, frayed but strong. It remembers holding things.",
            Tags = new List<string> { "tool" }
        }, cancellationToken);
    }

    private async Task SeedDifficultyProfilesAsync(CancellationToken cancellationToken)
    {
        // Keys must be stable; values are tweakable.
        var profiles = new List<DifficultyProfile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Key = "casual",
                Name = "Casual",
                Description = "Story-first. Generous resources. Minimal pressure.",
                StartingHealth = 100,
                StartingSanity = 100,
                LootMultiplier = 1.25f,
                EnemySpawnMultiplier = 0.75f,
                SanityDrainMultiplier = 0.75f,
                MinRooms = 7,
                MaxRooms = 9,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "novice",
                Name = "Novice",
                Description = "Forgiving, but the Night still bites.",
                StartingHealth = 100,
                StartingSanity = 100,
                LootMultiplier = 1.15f,
                EnemySpawnMultiplier = 0.9f,
                SanityDrainMultiplier = 0.9f,
                MinRooms = 7,
                MaxRooms = 10,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "easy",
                Name = "Easy",
                Description = "A gentle descent. Good for learning.",
                StartingHealth = 100,
                StartingSanity = 100,
                LootMultiplier = 1.05f,
                EnemySpawnMultiplier = 1.0f,
                SanityDrainMultiplier = 1.0f,
                MinRooms = 7,
                MaxRooms = 10,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "normal",
                Name = "Normal",
                Description = "The intended experience.",
                StartingHealth = 100,
                StartingSanity = 100,
                LootMultiplier = 1.0f,
                EnemySpawnMultiplier = 1.0f,
                SanityDrainMultiplier = 1.0f,
                MinRooms = 7,
                MaxRooms = 10,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "challenging",
                Name = "Challenging",
                Description = "Sharper claws, slimmer margins.",
                StartingHealth = 95,
                StartingSanity = 95,
                LootMultiplier = 0.95f,
                EnemySpawnMultiplier = 1.15f,
                SanityDrainMultiplier = 1.15f,
                MinRooms = 8,
                MaxRooms = 11,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "hard",
                Name = "Hard",
                Description = "The House notices you.",
                StartingHealth = 90,
                StartingSanity = 90,
                LootMultiplier = 0.9f,
                EnemySpawnMultiplier = 1.3f,
                SanityDrainMultiplier = 1.3f,
                MinRooms = 8,
                MaxRooms = 12,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "very-hard",
                Name = "Very Hard",
                Description = "A slow grind of dread.",
                StartingHealth = 85,
                StartingSanity = 85,
                LootMultiplier = 0.85f,
                EnemySpawnMultiplier = 1.45f,
                SanityDrainMultiplier = 1.45f,
                MinRooms = 9,
                MaxRooms = 13,
                IsEndless = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "endless",
                Name = "Endless",
                Description = "No map edge. No mercy. No guarantee of return.",
                StartingHealth = 90,
                StartingSanity = 90,
                LootMultiplier = 0.9f,
                EnemySpawnMultiplier = 1.35f,
                SanityDrainMultiplier = 1.35f,
                MinRooms = 9,
                MaxRooms = 14,
                IsEndless = true
            }
        };

        foreach (var p in profiles)
        {
            var existing = await _db.DifficultyProfiles.FirstOrDefaultAsync(x => x.Key == p.Key, cancellationToken);
            if (existing is null)
            {
                _db.DifficultyProfiles.Add(p);
            }
            else
            {
                // Keep user-tuned values if they already exist; only fill missing description/name.
                if (string.IsNullOrWhiteSpace(existing.Name)) existing.Name = p.Name;
                if (string.IsNullOrWhiteSpace(existing.Description)) existing.Description = p.Description;
            }
        }
    }

    private async Task SeedDialogueAsync(CancellationToken cancellationToken)
    {
        // Main story chapter 1 start.
        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "story.chapter01.start",
            Speaker = "The House",
            Text = "The world outside is darker than it should be — not night, but something older. Your last memory is daylight. That feels like a lie now."
        }, cancellationToken);

        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "story.chapter01.choice",
            Speaker = "The House",
            Text = "In your pocket: a warm vial. On the floor: a small lantern. The air tastes like pennies. What do you take?"
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "story.chapter01.start",
            Text = "Listen to the silence.",
            ToNodeKey = "story.chapter01.choice",
            SanityDelta = -1
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "story.chapter01.choice",
            Text = "Take the lantern.",
            ToNodeKey = null,
            MoralityDelta = 1,
            RevealDisposition = null
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "story.chapter01.choice",
            Text = "Drink the warm vial (it steadies you).",
            ToNodeKey = null,
            HealthDelta = 5,
            SanityDelta = 2,
            MoralityDelta = 1
        }, cancellationToken);

        // Keep existing encounter nodes below.
        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "story.intro.1",
            Speaker = "The House",
            Text = "Darkness has weight here. It presses on your eyelids even when they're open. Somewhere far above, a clock ticks without hands."
        }, cancellationToken);

        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "encounter.stranger.1",
            Speaker = "???",
            Text = "A figure stands in the corner of the room. You can't tell if it's breathing... or listening."
        }, cancellationToken);

        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "encounter.stranger.friendly",
            Speaker = "Stranger",
            Text = "Don't panic. The dark wants that. Are you hurt?"
        }, cancellationToken);

        await UpsertDialogueNodeAsync(new DialogueNode
        {
            Id = Guid.NewGuid(),
            Key = "encounter.stranger.hostile",
            Speaker = "Stranger",
            Text = "You smell like light. The Night hates light. And I hate what it hates."
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "encounter.stranger.1",
            Text = "Speak calmly: 'I don't want trouble.'",
            ToNodeKey = "encounter.stranger.friendly",
            MoralityDelta = 2,
            SanityDelta = 1,
            RevealDisposition = ActorDisposition.Friendly
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "encounter.stranger.1",
            Text = "Threaten it: 'Come closer and I'll end you.'",
            ToNodeKey = "encounter.stranger.hostile",
            MoralityDelta = -4,
            SanityDelta = -1,
            RevealDisposition = ActorDisposition.Hostile
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "encounter.stranger.hostile",
            Text = "Try to save it (spend sanity to pacify).",
            ToNodeKey = null,
            RequireMinSanity = 25,
            SanityDelta = -10,
            MoralityDelta = 6,
            PacifyTarget = true
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "encounter.stranger.friendly",
            Text = "Accept help.",
            ToNodeKey = null,
            HealthDelta = 10,
            MoralityDelta = 1,
            GrantItemKey = "bandage",
            GrantItemQuantity = 1
        }, cancellationToken);

        await UpsertDialogueChoiceAsync(new DialogueChoice
        {
            Id = Guid.NewGuid(),
            FromNodeKey = "encounter.stranger.friendly",
            Text = "Leave it and move on.",
            ToNodeKey = null
        }, cancellationToken);
    }

    private async Task SeedStoryChaptersAsync(CancellationToken cancellationToken)
    {
        await UpsertStoryChapterAsync(new StoryChapter
        {
            Id = Guid.NewGuid(),
            Key = "chapter.01.awakening",
            Title = "Awakening",
            StartNodeKey = "story.chapter01.start",
            Order = 1
        }, cancellationToken);
    }

    private async Task UpsertStoryChapterAsync(StoryChapter chapter, CancellationToken cancellationToken)
    {
        var existing = await _db.StoryChapters.FirstOrDefaultAsync(c => c.Key == chapter.Key, cancellationToken);
        if (existing is null)
        {
            _db.StoryChapters.Add(chapter);
            return;
        }

        existing.Title = chapter.Title;
        existing.StartNodeKey = chapter.StartNodeKey;
        existing.Order = chapter.Order;
        _db.StoryChapters.Update(existing);
    }

    private async Task UpsertRoomAsync(Room room, CancellationToken cancellationToken)
    {
        var existing = await _db.Rooms.FirstOrDefaultAsync(r => r.Key == room.Key, cancellationToken);
        if (existing is null)
        {
            _db.Rooms.Add(room);
            return;
        }

        existing.Name = room.Name;
        existing.Description = room.Description;
        existing.Exits = room.Exits;
        _db.Rooms.Update(existing);
    }

    private async Task UpsertItemDefinitionAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        var existing = await _db.ItemDefinitions.FirstOrDefaultAsync(i => i.Key == item.Key, cancellationToken);
        if (existing is null)
        {
            _db.ItemDefinitions.Add(item);
            return;
        }

        existing.Name = item.Name;
        existing.Description = item.Description;
        existing.Tags = item.Tags;
        _db.ItemDefinitions.Update(existing);
    }

    private async Task UpsertDialogueNodeAsync(DialogueNode node, CancellationToken cancellationToken)
    {
        var existing = await _db.DialogueNodes.FirstOrDefaultAsync(n => n.Key == node.Key, cancellationToken);
        if (existing is null)
        {
            _db.DialogueNodes.Add(node);
            return;
        }

        existing.Speaker = node.Speaker;
        existing.Text = node.Text;
        existing.Tags = node.Tags;
        _db.DialogueNodes.Update(existing);
    }

    private async Task UpsertDialogueChoiceAsync(DialogueChoice choice, CancellationToken cancellationToken)
    {
        // Choices currently identified by (FromNodeKey, Text). Good enough for early iteration.
        var existing = await _db.DialogueChoices
            .FirstOrDefaultAsync(c => c.FromNodeKey == choice.FromNodeKey && c.Text == choice.Text, cancellationToken);

        if (existing is null)
        {
            _db.DialogueChoices.Add(choice);
            return;
        }

        existing.ToNodeKey = choice.ToNodeKey;
        existing.RequireMinMorality = choice.RequireMinMorality;
        existing.RequireMaxMorality = choice.RequireMaxMorality;
        existing.RequireMinSanity = choice.RequireMinSanity;
        existing.SanityDelta = choice.SanityDelta;
        existing.HealthDelta = choice.HealthDelta;
        existing.MoralityDelta = choice.MoralityDelta;
        existing.RevealDisposition = choice.RevealDisposition;
        existing.PacifyTarget = choice.PacifyTarget;
        existing.GrantItemKey = choice.GrantItemKey;
        existing.GrantItemQuantity = choice.GrantItemQuantity;
        _db.DialogueChoices.Update(existing);
    }

    private async Task SeedLorePacksAsync(CancellationToken cancellationToken)
    {
        var packs = new List<LorePack>
        {
            new() { Id = Guid.NewGuid(), Key = "cosmic-horror", Name = "Cosmic Horror", StyleTags = "cosmic;horror;dread" },
            new() { Id = Guid.NewGuid(), Key = "lovecraft", Name = "Lovecraftian", StyleTags = "eldritch;antiquarian;madness" },
            new() { Id = Guid.NewGuid(), Key = "zork", Name = "Parser Classic", StyleTags = "parser;adventure;retro" },
            new() { Id = Guid.NewGuid(), Key = "undertale", Name = "Quirky Meta", StyleTags = "meta;whimsy;bittersweet" },
        };

        foreach (var p in packs)
        {
            var existing = await _db.LorePacks.FirstOrDefaultAsync(x => x.Key == p.Key, cancellationToken);
            if (existing is null)
                _db.LorePacks.Add(p);
            else
            {
                existing.Name = p.Name;
                existing.StyleTags = p.StyleTags;
                _db.LorePacks.Update(existing);
            }
        }
    }

    private async Task SeedDialogueSnippetsAsync(CancellationToken cancellationToken)
    {
        // Minimal seed set; expand over time.
        var snippets = new List<DialogueSnippet>
        {
            // Pack-agnostic
            new()
            {
                Id = Guid.NewGuid(),
                Key = "base.opening.001",
                Role = "opening",
                Tags = "encounter;room",
                Weight = 8,
                Text = "The air in {room} tastes like {fearWord}."
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "base.middle.001",
                Role = "middle",
                Tags = "encounter",
                Weight = 6,
                Text = "You feel watched—politely. Like something is waiting its turn."
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "base.closing.001",
                Role = "closing",
                Tags = "encounter",
                Weight = 6,
                Text = "Your name, {player}, sounds unfamiliar in your own mouth."
            },

            // Lovecraft
            new()
            {
                Id = Guid.NewGuid(),
                Key = "lovecraft.opening.001",
                PackKey = "lovecraft",
                Role = "opening",
                Tags = "encounter;cosmic",
                Weight = 10,
                Text = "There is an angle in {room} that refuses to be measured."
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "lovecraft.middle.lowSanity.001",
                PackKey = "lovecraft",
                Role = "middle",
                Tags = "encounter;cosmic",
                Weight = 12,
                MaxSanity = 30,
                Text = "A thought crawls across your mind like a pale insect: it knows your shape."
            },

            // Zork-ish (homage)
            new()
            {
                Id = Guid.NewGuid(),
                Key = "zork.opening.001",
                PackKey = "zork",
                Role = "opening",
                Tags = "encounter;room",
                Weight = 8,
                Text = "It is pitch dark. You are likely to be eaten by a {fearWord}."
            },

            // Undertale-ish (homage)
            new()
            {
                Id = Guid.NewGuid(),
                Key = "undertale.middle.001",
                PackKey = "undertale",
                Role = "middle",
                Tags = "encounter",
                Weight = 6,
                Text = "A small part of you wonders if the darkness is just... shy."
            },
            new()
            {
                Id = Guid.NewGuid(),
                Key = "undertale.closing.lowSanity.001",
                PackKey = "undertale",
                Role = "closing",
                Tags = "encounter",
                Weight = 9,
                MaxSanity = 35,
                Text = "You laugh once, quietly. The sound doesn't match the room."
            },
        };

        foreach (var s in snippets)
            await UpsertDialogueSnippetAsync(s, cancellationToken);
    }

    private async Task UpsertDialogueSnippetAsync(DialogueSnippet snippet, CancellationToken cancellationToken)
    {
        var existing = await _db.DialogueSnippets.FirstOrDefaultAsync(x => x.Key == snippet.Key, cancellationToken);
        if (existing is null)
        {
            _db.DialogueSnippets.Add(snippet);
            return;
        }

        existing.Text = snippet.Text;
        existing.Tags = snippet.Tags;
        existing.Weight = snippet.Weight;
        existing.PackKey = snippet.PackKey;
        existing.MinSanity = snippet.MinSanity;
        existing.MaxSanity = snippet.MaxSanity;
        existing.MinMorality = snippet.MinMorality;
        existing.MaxMorality = snippet.MaxMorality;
        existing.RequiredDisposition = snippet.RequiredDisposition;
        existing.Role = snippet.Role;
        _db.DialogueSnippets.Update(existing);
    }
}
