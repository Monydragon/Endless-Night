using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
using EndlessNight.Domain.Story;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EndlessNight.Persistence;

public sealed class SqliteDbContext : DbContext
{
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options) : base(options)
    {
    }

    public static void ClearAllPools()
    {
        // Helps avoid 'file in use' on Windows when deleting the SQLite database.
        SqliteConnection.ClearAllPools();
    }

    // Legacy template content
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<MapDefinition> Maps => Set<MapDefinition>();
    public DbSet<SaveGame> SaveGames => Set<SaveGame>();

    // Content definitions (new)
    public DbSet<ItemDefinition> ItemDefinitions => Set<ItemDefinition>();

    // Dialogue content + run state
    public DbSet<DialogueNode> DialogueNodes => Set<DialogueNode>();
    public DbSet<DialogueChoice> DialogueChoices => Set<DialogueChoice>();
    public DbSet<RunDialogueState> RunDialogueStates => Set<RunDialogueState>();
    public DbSet<LorePack> LorePacks => Set<LorePack>();
    public DbSet<DialogueSnippet> DialogueSnippets => Set<DialogueSnippet>();
    public DbSet<LoreWord> LoreWords => Set<LoreWord>();

    // Procedural runs
    public DbSet<RunState> Runs => Set<RunState>();
    public DbSet<RoomInstance> RoomInstances => Set<RoomInstance>();
    public DbSet<RunInventoryItem> RunInventoryItems => Set<RunInventoryItem>();
    public DbSet<ActorInstance> ActorInstances => Set<ActorInstance>();
    public DbSet<RoomEventLog> RoomEventLogs => Set<RoomEventLog>();
    public DbSet<WorldObjectInstance> WorldObjects => Set<WorldObjectInstance>();

    // Main story content + run state
    public DbSet<StoryChapter> StoryChapters => Set<StoryChapter>();
    public DbSet<RunStoryState> RunStoryStates => Set<RunStoryState>();

    // Difficulty profiles (data-driven)
    public DbSet<DifficultyProfile> DifficultyProfiles => Set<DifficultyProfile>();

    // Per-run configuration
    public DbSet<RunConfig> RunConfigs => Set<RunConfig>();

    // Global settings + world/run memory
    public DbSet<GameSetting> GameSettings => Set<GameSetting>();
    public DbSet<RunFlag> RunFlags => Set<RunFlag>();
    public DbSet<WorldFlag> WorldFlags => Set<WorldFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MapDefinition
        modelBuilder.Entity<MapDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<MapDefinition>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<MapDefinition>().HasIndex(x => x.Key).IsUnique();

        // Room (legacy)
        modelBuilder.Entity<Room>().HasKey(x => x.Id);
        modelBuilder.Entity<Room>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<Room>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<Room>()
            .Property(x => x.Exits)
            .HasConversion(new StringDictionaryJsonConverter());

        // SaveGame (legacy)
        modelBuilder.Entity<SaveGame>().HasKey(x => x.Id);
        modelBuilder.Entity<SaveGame>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<SaveGame>().HasIndex(x => new { x.PlayerName, x.MapKey });

        // ItemDefinition
        modelBuilder.Entity<ItemDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<ItemDefinition>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<ItemDefinition>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<ItemDefinition>()
            .Property(x => x.Tags)
            .HasConversion(new StringListJsonConverter());

        // DifficultyProfile
        modelBuilder.Entity<DifficultyProfile>().HasKey(x => x.Id);
        modelBuilder.Entity<DifficultyProfile>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<DifficultyProfile>().HasIndex(x => x.Key).IsUnique();

        // RunState
        modelBuilder.Entity<RunState>().HasKey(x => x.Id);
        modelBuilder.Entity<RunState>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunState>().HasIndex(x => x.RunId).IsUnique();
        modelBuilder.Entity<RunState>().HasIndex(x => x.PlayerName);

        // RoomInstance
        modelBuilder.Entity<RoomInstance>().HasKey(x => x.Id);
        modelBuilder.Entity<RoomInstance>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RoomInstance>().HasIndex(x => new { x.RunId, x.RoomId }).IsUnique();
        modelBuilder.Entity<RoomInstance>()
            .Property(x => x.Exits)
            .HasConversion(new RoomExitsConverter());
        modelBuilder.Entity<RoomInstance>()
            .Property(x => x.Loot)
            .HasConversion(new StringListJsonConverter());
        modelBuilder.Entity<RoomInstance>()
            .Property(x => x.RoomTags)
            .HasConversion(new StringListJsonConverter());

        // RunInventoryItem
        modelBuilder.Entity<RunInventoryItem>().HasKey(x => x.Id);
        modelBuilder.Entity<RunInventoryItem>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunInventoryItem>()
            .HasIndex(x => new { x.RunId, x.ItemKey })
            .IsUnique();

        // ActorInstance
        modelBuilder.Entity<ActorInstance>().HasKey(x => x.Id);
        modelBuilder.Entity<ActorInstance>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<ActorInstance>()
            .HasIndex(x => new { x.RunId, x.CurrentRoomId });
        modelBuilder.Entity<ActorInstance>()
            .HasIndex(x => new { x.RunId, x.CurrentRoomId, x.SpawnIndex });
        modelBuilder.Entity<ActorInstance>()
            .HasIndex(x => new { x.RunId, x.Kind, x.PacifyUnlocked });

        // RoomEventLog
        modelBuilder.Entity<RoomEventLog>().HasKey(x => x.Id);
        modelBuilder.Entity<RoomEventLog>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RoomEventLog>()
            .HasIndex(x => new { x.RunId, x.Turn });
        modelBuilder.Entity<RoomEventLog>()
            .HasIndex(x => new { x.RunId, x.ActorId });

        // DialogueNode
        modelBuilder.Entity<DialogueNode>().HasKey(x => x.Id);
        modelBuilder.Entity<DialogueNode>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<DialogueNode>().HasIndex(x => x.Key).IsUnique();

        // DialogueChoice
        modelBuilder.Entity<DialogueChoice>().HasKey(x => x.Id);
        modelBuilder.Entity<DialogueChoice>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<DialogueChoice>().HasIndex(x => x.FromNodeKey);
        // GrantItemKey/GrantItemQuantity are simple scalar columns.

        // LorePack
        modelBuilder.Entity<LorePack>().HasKey(x => x.Id);
        modelBuilder.Entity<LorePack>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<LorePack>().HasIndex(x => x.Key).IsUnique();

        // DialogueSnippet
        modelBuilder.Entity<DialogueSnippet>().HasKey(x => x.Id);
        modelBuilder.Entity<DialogueSnippet>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<DialogueSnippet>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<DialogueSnippet>().HasIndex(x => x.PackKey);
        modelBuilder.Entity<DialogueSnippet>().HasIndex(x => x.Role);

        // RunDialogueState
        modelBuilder.Entity<RunDialogueState>().HasKey(x => x.Id);
        modelBuilder.Entity<RunDialogueState>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunDialogueState>()
            .HasIndex(x => new { x.RunId, x.ActorId })
            .IsUnique();

        // StoryChapter
        modelBuilder.Entity<StoryChapter>().HasKey(x => x.Id);
        modelBuilder.Entity<StoryChapter>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<StoryChapter>().HasIndex(x => x.Key).IsUnique();
        modelBuilder.Entity<StoryChapter>().HasIndex(x => x.Order);

        // RunStoryState
        modelBuilder.Entity<RunStoryState>().HasKey(x => x.Id);
        modelBuilder.Entity<RunStoryState>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunStoryState>().HasIndex(x => x.RunId).IsUnique();
        modelBuilder.Entity<RunStoryState>()
            .Property(x => x.CompletedChapterKeys)
            .HasConversion(new StringListJsonConverter());

        // WorldObjectInstance
        modelBuilder.Entity<WorldObjectInstance>().HasKey(x => x.Id);
        modelBuilder.Entity<WorldObjectInstance>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<WorldObjectInstance>()
            .HasIndex(x => new { x.RunId, x.RoomId });
        modelBuilder.Entity<WorldObjectInstance>()
            .HasIndex(x => new { x.RunId, x.Kind });
        modelBuilder.Entity<WorldObjectInstance>()
            .Property(x => x.LootItemKeys)
            .HasConversion(new StringListJsonConverter());

        // RunConfig
        modelBuilder.Entity<RunConfig>().HasKey(x => x.Id);
        modelBuilder.Entity<RunConfig>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunConfig>().HasIndex(x => x.RunId).IsUnique();
        modelBuilder.Entity<RunConfig>()
            .Property(x => x.EnabledLorePacks)
            .HasConversion(new StringListJsonConverter());

        // GameSetting (global)
        modelBuilder.Entity<GameSetting>().HasKey(x => x.Id);
        modelBuilder.Entity<GameSetting>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<GameSetting>().HasIndex(x => x.Key).IsUnique();

        // RunFlag (per-run memory)
        modelBuilder.Entity<RunFlag>().HasKey(x => x.Id);
        modelBuilder.Entity<RunFlag>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<RunFlag>().HasIndex(x => new { x.RunId, x.ScopeType, x.ScopeId, x.Key }).IsUnique();

        // WorldFlag (cross-run memory)
        modelBuilder.Entity<WorldFlag>().HasKey(x => x.Id);
        modelBuilder.Entity<WorldFlag>()
            .Property(x => x.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<WorldFlag>().HasIndex(x => x.Key).IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
