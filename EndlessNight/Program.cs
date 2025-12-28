using EndlessNight.Domain;
using EndlessNight.Persistence;
using EndlessNight.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace EndlessNight;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Configure console window for optimal display
        ConsoleConfig.ConfigureConsoleWindow(configuration);

        // Initialize controller support
        ControllerUI.InitializeController();

        AnsiConsole.MarkupLine("[bold yellow]Endless Night[/]");
        AnsiConsole.MarkupLine("[grey]Goal: Find the House's heart and escape the Night.[/]");

        var sqliteConnectionString =
            configuration.GetSection("Sqlite")["ConnectionString"] ??
            configuration["Sqlite__ConnectionString"] ??
            "Data Source=endless-night.db";

        try
        {
            using var db = SqliteDbContextFactory.Create(sqliteConnectionString, resetOnMismatch: false);

            await new Seeder(db).EnsureSeededAsync();

            var playerName = AnsiConsole.Ask<string>("Enter your [green]player name[/]:").Trim();
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player";

            var runService = new RunService(db, new ProceduralLevel1Generator());

            var run = await SelectOrCreateRunAsync(runService, playerName, sqliteConnectionString);
            if (run is null)
                return 0;

            await GameLoopAsync(runService, run);
            return 0;
        }
        catch (Microsoft.Data.Sqlite.SqliteException sex) when (sex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            // Attempt automatic DB recreation and reseed when schema mismatch occurs.
            AnsiConsole.MarkupLine("[yellow]SQLite reported missing tables. Attempting to recreate the database...[/]");
            if (!EndlessNight.Persistence.SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
            {
                AnsiConsole.MarkupLine("[red]Automatic reset failed.[/] You can manually delete the DB file or set env var [grey]Sqlite__ResetOnModelMismatch=true[/].");
                return 1;
            }

            using var db = EndlessNight.Persistence.SqliteDbContextFactory.Create(sqliteConnectionString, resetOnMismatch: true);
            await new Seeder(db).EnsureSeededAsync();
            AnsiConsole.MarkupLine("[green]Database recreated and seeded.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            AnsiConsole.MarkupLine("\n[red]Database problem?[/]");
            AnsiConsole.MarkupLine("This build uses a self-contained SQLite file database.");
            AnsiConsole.MarkupLine("You can set [grey]Sqlite:ConnectionString[/] in appsettings.json or env var [grey]Sqlite__ConnectionString[/].");
            AnsiConsole.MarkupLine("If you changed the data model, you can delete the DB file or set env var [grey]Sqlite__ResetOnModelMismatch=true[/].");
            return 1;
        }
    }

    private static async Task GameLoopAsync(RunService runService, RunState run)
    {
        while (true)
        {
            var room = await runService.GetCurrentRoomAsync(run);
            if (room is null)
            {
                AnsiConsole.MarkupLine("[red]The current room vanished. The Night is hungry.[/]");
                return;
            }

            // Resolve any on-entry traps before showing actions.
            await ResolveEntryTrapsAsync(runService, run);

            // Let NPCs auto-speak (if configured) after traps, before the action menu.
            await ShowNpcAutoTalkOnEntryAsync(runService, run);

            // Get visible objects for HUD display
            var visibleObjects = await runService.GetVisibleWorldObjectsInCurrentRoomAsync(run);

            // Render the new dynamic HUD
            GameHUD.RenderFullHUD(run, room, visibleObjects);

            // Dynamically determine available actions based on room state
            var availableActions = await GetAvailableActionsAsync(runService, run, room);
            
            // Create action descriptions
            var actionDescriptions = new Dictionary<string, string>
            {
                { "Move", "Navigate to an adjacent room" },
                { "Search Room", "Search for hidden items and triggers" },
                { "Interact", "Use objects in the room" },
                { "Talk", "Speak to someone in the room" },
                { "Encounter", "Spare / pacify enemies" },
                { "Inventory", "View your collected items" },
                { "Rest (Campfire)", "Restore health and sanity" },
                { "Toggle Debug", "Enable/disable debug information" },
                { "Quit", "Exit the game" }
            };

            var actionOptions = availableActions
                .Select(a => (
                    option: a,
                    description: actionDescriptions.TryGetValue(a, out var desc) ? desc : ""
                ))
                .ToList();

            var (choice, _) = ControllerUI.SelectFromMenuWithHUD(
                "AVAILABLE ACTIONS",
                actionOptions,
                run,
                room,
                visibleObjects
            );

            // Route to appropriate handler
            if (choice == "Quit")
                return;

            if (choice == "Toggle Debug")
            {
                runService.DebugMode = !runService.DebugMode;
                GameHUD.DebugMode = runService.DebugMode;
                ControllerUI.ShowSuccess($"Debug {(runService.DebugMode ? "ON" : "OFF")}");
                continue;
            }

            if (choice == "Inventory")
            {
                await ShowInventoryAsync(runService, run);
                continue;
            }

            if (choice == "Search Room")
            {
                var (ok, msg) = await runService.SearchRoomAsync(run);
                if (ok)
                    ControllerUI.ShowSuccess(msg);
                else
                    ControllerUI.ShowError(msg);
                continue;
            }

            if (choice == "Rest (Campfire)")
            {
                var visible = await runService.GetVisibleWorldObjectsInCurrentRoomAsync(run);
                var camp = visible.FirstOrDefault(o => o.Kind == WorldObjectKind.Campfire);
                if (camp is null)
                {
                    ControllerUI.ShowError("No firepit here. The cold watches.");
                }
                else
                {
                    var (ok, msg) = await runService.InteractAsync(run, camp.Id);
                    if (ok)
                        ControllerUI.ShowSuccess(msg);
                    else
                        ControllerUI.ShowError(msg);
                }
                continue;
            }

            if (choice == "Interact")
            {
                await InteractMenuAsync(runService, run);
                continue;
            }

            if (choice == "Talk")
            {
                await TalkMenuAsync(runService, run);
                continue;
            }

            if (choice == "Move")
            {
                await MoveMenuAsync(runService, run, room);
                continue;
            }

            if (choice == "Encounter")
            {
                await EncounterMenuAsync(runService, run);
                continue;
            }
        }
    }

    private static async Task ShowNpcAutoTalkOnEntryAsync(RunService runService, RunState run)
    {
        // Pull any npc.auto events for the current turn and show them like a short cutscene.
        // We keep it non-blocking: if there are none, it does nothing.
        var lines = await runService.GetNpcAutoTalkLinesForCurrentTurnAsync(run);
        if (lines.Count == 0)
            return;

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]VOICES IN THE ROOM[/]");
        AnsiConsole.WriteLine();
        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine($"[grey]•[/] {ControllerUI.EscapeMarkup(line)}");
        }
        ControllerUI.WaitToContinue("Press A/Enter...");

        // If any NPC auto-talk lines were generated this turn, surface them.
        // Remove direct DB access; RunService provides helper.
        var autoLines = await runService.GetNpcAutoTalkLinesForCurrentTurnAsync(run);
        foreach (var msg in autoLines)
            AnsiConsole.MarkupLine(ControllerUI.EscapeMarkup(msg));
    }

    private static async Task<List<string>> GetAvailableActionsAsync(RunService runService, RunState run, RoomInstance room)
    {
        var actions = new List<string> { "Move" };

        // Check for searchable items
        var hidden = await runService.GetHiddenWorldObjectsInCurrentRoomAsync(run);
        if (hidden.Count > 0 && !room.HasBeenSearched)
        {
            actions.Add("Search Room");
        }

        // Check for interactable objects
        var visible = await runService.GetVisibleWorldObjectsInCurrentRoomAsync(run);
        if (visible.Count > 0)
        {
            actions.Add("Interact");
        }

        // Talk if any actors are present
        var actors = await runService.GetActorsInCurrentRoomAsync(run);
        if (actors.Count > 0)
            actions.Add("Talk");

        // Encounter if any enemies are present
        var enemies = await runService.GetEnemiesInCurrentRoomAsync(run);
        if (enemies.Count > 0)
            actions.Add("Encounter");

        // Always show inventory
        actions.Add("Inventory");

        // Check for campfire
        var campfire = visible.FirstOrDefault(o => o.Kind == WorldObjectKind.Campfire);
        if (campfire is not null)
        {
            actions.Add("Rest (Campfire)");
        }

        // Debug option
        actions.Add("Toggle Debug");
        actions.Add("Quit");

        return actions;
    }

    private static void RenderActionMenu(List<string> actions)
    {
        var actionDescriptions = new Dictionary<string, string>
        {
            { "Move", "Navigate to an adjacent room" },
            { "Search Room", "Search for hidden items and triggers" },
            { "Interact", "Use objects in the room" },
            { "Inventory", "View your collected items" },
            { "Rest (Campfire)", "Restore health and sanity" },
            { "Toggle Debug", "Enable/disable debug information" },
            { "Quit", "Exit the game" }
        };

        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold cyan]AVAILABLE ACTIONS[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        foreach (var action in actions)
        {
            if (actionDescriptions.TryGetValue(action, out var desc))
            {
                AnsiConsole.MarkupLine($"[cyan]▸ {action}[/] - [dim]{desc}[/]");
            }
        }
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        AnsiConsole.WriteLine();
    }

    private static async Task ShowInventoryAsync(RunService runService, RunState run)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold cyan]INVENTORY[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        
        var inv = await runService.GetInventoryAsync(run);
        if (inv.Count == 0)
        {
            ControllerUI.ShowInfo("Your pockets are a rumor.");
        }
        else
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan]Item[/]")
                .AddColumn("[cyan]Qty[/]");
            foreach (var i in inv)
                table.AddRow(i.ItemKey, i.Quantity.ToString());
            AnsiConsole.Write(table);
        }
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        ControllerUI.WaitToContinue();
    }

    private static void RenderRoomBanner(RoomInstance room, RunState run)
    {
        // Color the room name by danger level
        var roomColor = room.DangerRating switch
        {
            >= 3 => "red",
            >= 2 => "yellow",
            >= 1 => "blue",
            _ => "green"
        };

        var panel = new Panel(new Markup($"[bold underline {roomColor}]{EscapeMarkup(room.Name)}[/]\n[dim]({room.X}, {room.Y})[/]  [bold orange3]⚠ Danger:[/] [bold {(room.DangerRating >= 2 ? "red" : "yellow")}]{room.DangerRating}[/]"))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1, 1, 1),
            BorderStyle = new Style(foreground: Color.FromConsoleColor(ConsoleColor.DarkGray))
        };
        AnsiConsole.Write(panel);

        // Exposition with atmospheric color based on sanity
        var sanity = run.Sanity;
        var line = sanity switch
        {
            >= 80 => "[green]The walls behave. Mostly.[/]",
            >= 60 => "[yellow]Something watches with polite hunger.[/]",
            >= 40 => "[cyan]Angles disagree about being angles.[/]",
            >= 20 => "[magenta]You keep seeing doors that won't admit to being doors.[/]",
            _ => "[bold red]Reality is threadbare here. Your breath draws patterns that don't hold.[/]"
        };
        AnsiConsole.MarkupLine(line);

        // Color-coded stats: Health first, then Sanity, then Morality, then Turn
        var healthColor = run.Health switch
        {
            >= 75 => "green",
            >= 50 => "yellow",
            >= 25 => "orange3",
            _ => "red"
        };
        var sanityColor = run.Sanity switch
        {
            >= 75 => "green",
            >= 50 => "cyan",
            >= 25 => "magenta",
            _ => "red"
        };
        var moralityColor = run.Morality switch
        {
            > 0 => "green",
            < 0 => "red",
            _ => "grey"
        };

        var moralitySymbol = run.Morality > 0 ? "↑" : run.Morality < 0 ? "↓" : "→";
        AnsiConsole.MarkupLine($"[bold cyan]❤ Health:[/] [bold {healthColor}]{run.Health}[/]  [bold cyan]⚡ Sanity:[/] [bold {sanityColor}]{run.Sanity}[/]  [bold cyan]⚖ Morality:[/] [bold {moralityColor}]{moralitySymbol} {run.Morality}[/]  [bold cyan]🔄 Turn:[/] [bold white]{run.Turn}[/]");
    }

    private static async Task InteractMenuAsync(RunService runService, RunState run)
    {
        var objects = await runService.GetVisibleWorldObjectsInCurrentRoomAsync(run);
        if (objects.Count == 0)
        {
            ControllerUI.ShowInfo("Nothing here seems usable.");
            return;
        }

        // Create menu options with descriptions
        var menuOptions = new List<(string option, string description)>();
        var objectMap = new Dictionary<string, WorldObjectInstance>();

        foreach (var o in objects)
        {
            var displayName = o.Kind switch
            {
                WorldObjectKind.GroundItem => $"Pick up: {o.ItemKey}",
                WorldObjectKind.Chest => o.IsOpened ? "📦 Chest (opened)" : "📦 Chest (locked)",
                WorldObjectKind.Trap => o.IsDisarmed ? "⚠ Trap (disarmed)" : "⚠ Trap (armed)",
                WorldObjectKind.PuzzleGate => o.IsSolved ? $"🔓 {o.Name} (open)" : $"🔒 {o.Name}",
                WorldObjectKind.Campfire => "🔥 Firepit (rest here)",
                _ => o.Name
            };
            
            menuOptions.Add((displayName, o.Description ?? ""));
            objectMap[displayName] = o;
        }

        menuOptions.Add(("🔙 Back", "Return to previous menu"));

        var (selected, _) = ControllerUI.SelectFromMenuWithDescriptions(
            "INTERACT WITH",
            menuOptions
        );

        if (selected == "🔙 Back")
            return;

        if (!objectMap.TryGetValue(selected, out var selectedObject))
            return;

        var (ok, msg) = await runService.InteractAsync(run, selectedObject.Id);
        if (ok)
            ControllerUI.ShowSuccess(msg);
        else
            ControllerUI.ShowError(msg);
    }

    private static async Task<RunState?> SelectOrCreateRunAsync(RunService runService, string playerName,
        string sqliteConnectionString)
    {
        while (true)
        {
            AnsiConsole.Clear();

            // Main menu decoration (riddle / vibe)
            AnsiConsole.Write(new Rule("[bold cyan]WELCOME[/]").RuleStyle("cyan").Centered());
            AnsiConsole.MarkupLine("[bold]A riddle from the dark:[/]");
            AnsiConsole.MarkupLine("[italic]\"In a house with no windows, I still show you the way.\nI speak in single words, yet I never lie.\nTake me, and the night becomes a map.\"[/]");
            AnsiConsole.MarkupLine("[dim]Type a name, choose a path, and listen for the parser in your bones.[/]");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[dim]Tip: Try thinking like a classic text adventure: LOOK, TAKE, OPEN, USE...[/]");
            AnsiConsole.WriteLine();

            var choices = new List<string> 
            { 
                "Continue", "New Game", "New Game (Seeded)",
                "Test Lab (Debug)",
                "Inspect Saves (Debug)", "Reset DB (Debug)", 
                "Recreate DB (Fix tables)", "Quit" 
            };
            
            var choice = ControllerUI.SelectFromMenu("Main Menu", choices);

            if (choice == "Quit")
                return null;

            if (choice == "Test Lab (Debug)")
            {
                var run = await CreateNewRunWithDifficultyAsync(runService, playerName, sqliteConnectionString, seed: 12345);
                await RunTestLabAsync(runService, run);
                // After leaving the lab, return to menu.
                continue;
            }

            if (choice == "Reset DB (Debug)")
            {
                var confirm = ControllerUI.Confirm("This will [red]delete[/] your SQLite DB file and all saves. Continue?");
                if (!confirm)
                    continue;

                if (!EndlessNight.Persistence.SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
                {
                    AnsiConsole.MarkupLine("[red]Could not reset the DB.[/] Only simple 'Data Source=...' connection strings are supported.");
                    Pause();
                    continue;
                }

                AnsiConsole.MarkupLine("[green]Database deleted.[/] Restarting will recreate it.");
                Pause();
                Environment.Exit(0);
            }

            if (choice == "Recreate DB (Fix tables)")
            {
                var confirm = ControllerUI.Confirm("This will [red]delete[/] your SQLite DB file and recreate tables and data. Continue?");
                if (!confirm)
                    continue;

                if (!EndlessNight.Persistence.SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
                {
                    AnsiConsole.MarkupLine("[red]Could not reset the DB.[/] Only simple 'Data Source=...' connection strings are supported.");
                    Pause();
                    continue;
                }

                using var db = EndlessNight.Persistence.SqliteDbContextFactory.Create(sqliteConnectionString, resetOnMismatch: true);
                await new Seeder(db).EnsureSeededAsync();
                AnsiConsole.MarkupLine("[green]Database recreated and seeded.[/]");
                Pause();
                continue;
            }

            if (choice == "New Game")
            {
                var run = await CreateNewRunWithDifficultyAsync(runService, playerName, sqliteConnectionString, seed: null);
                await ShowIntroDialogueAsync(run);
                return run;
            }

            if (choice == "New Game (Seeded)")
            {
                var seedText = AnsiConsole.Ask<string>("Enter a seed ([grey]int[/]) or leave blank for random:").Trim();
                int? seed = null;
                if (!string.IsNullOrWhiteSpace(seedText) && int.TryParse(seedText, out var parsed))
                    seed = parsed;

                var run = await CreateNewRunWithDifficultyAsync(runService, playerName, sqliteConnectionString, seed);
                await ShowIntroDialogueAsync(run);
                return run;
            }

            if (choice == "Inspect Saves (Debug)")
            {
                await InspectSavesAsync(runService, playerName);
                continue;
            }

            // Continue
            var runs = await runService.GetRunsAsync(playerName);
            if (runs.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No saves found. Starting a new run...[/]");
                var run = await CreateNewRunWithDifficultyAsync(runService, playerName, sqliteConnectionString, seed: null);
                await ShowIntroDialogueAsync(run);
                return run;
            }

            var runChoice = ControllerUI.SelectFromList(
                "Select a save",
                runs,
                r => $"Run {r.RunId} | Turn {r.Turn} | Sanity {r.Sanity} | Health {r.Health} | Morality {r.Morality} | Updated {r.UpdatedUtc:u}");

            return runChoice;
        }
    }

    private static async Task<RunState> CreateNewRunWithDifficultyAsync(
        RunService runService,
        string playerName,
        string sqliteConnectionString,
        int? seed)
    {
        // Difficulty selection (simple version)
        // IMPORTANT: use the same configured DB file and reset-on-mismatch behavior,
        // otherwise an older DB file will throw "no such column" for newly added fields.
        var resetOnMismatchFromEnv =
            bool.TryParse(Environment.GetEnvironmentVariable("Sqlite__ResetOnModelMismatch"), out var parsed) && parsed;

#if DEBUG
        // In debug builds, prefer self-healing DB behavior to speed iteration.
        var resetOnMismatch = resetOnMismatchFromEnv || true;
#else
        var resetOnMismatch = resetOnMismatchFromEnv;
#endif

        SqliteDbContext dbTmp;
        try
        {
            dbTmp = SqliteDbContextFactory.Create(sqliteConnectionString, resetOnMismatch: resetOnMismatch);
        }
        catch (Microsoft.Data.Sqlite.SqliteException sex) when (sex.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
        {
            // Stale schema: rebuild it now.
            SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString);
            dbTmp = SqliteDbContextFactory.Create(sqliteConnectionString, resetOnMismatch: true);
        }

        using (dbTmp)
        {
            await new Seeder(dbTmp).EnsureSeededAsync();

            var diffService = new DifficultyService(dbTmp);
            var profiles = await diffService.GetAllAsync();

            var options = profiles
                .Select(p => (
                    option: p.Name,
                    description: string.IsNullOrWhiteSpace(p.Description) ? p.Key : $"{p.Description} (key: {p.Key})"))
                .ToList();

            var (_, idx) = ControllerUI.SelectFromMenuWithDescriptions("Select Difficulty", options);
            var selected = profiles[Math.Clamp(idx, 0, profiles.Count - 1)];

            return await runService.CreateNewRunAsync(playerName, seed, selected.Key);
        }
    }

    private static async Task RunTestLabAsync(RunService runService, RunState run)
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold cyan]TEST LAB[/]");
            AnsiConsole.MarkupLine("[dim]Quick iteration scene: spawn actors, advance turns, and watch the DB-backed movement/spawns.[/]");
            AnsiConsole.WriteLine();

            var room = await runService.GetCurrentRoomAsync(run);
            var actors = await runService.GetActorsInCurrentRoomAsync(run);

            AnsiConsole.MarkupLine($"[grey]Run[/]: {run.RunId}  [grey]Turn[/]: {run.Turn}  [grey]Room[/]: {ControllerUI.EscapeMarkup(room?.Name ?? "(unknown)")}");
            AnsiConsole.MarkupLine($"[grey]Actors here[/]: {actors.Count}");
            foreach (var a in actors)
                AnsiConsole.MarkupLine($"  - [cyan]{a.Kind}[/] {ControllerUI.EscapeMarkup(a.Name)} (Intensity {a.Intensity})");

            AnsiConsole.WriteLine();

            var choices = new List<string>
            {
                "Spawn NPC",
                "Spawn Enemy",
                "Populate every room (NPC/Enemy)",
                "Advance 1 turn",
                "Advance 5 turns",
                "Advance 20 turns",
                "Back"
            };

            var choice = ControllerUI.SelectFromMenu("Test Lab Actions", choices);

            switch (choice)
            {
                case "Spawn NPC":
                    await runService.ForceSpawnActorInCurrentRoomAsync(run, ActorKind.Npc);
                    break;
                case "Spawn Enemy":
                    await runService.ForceSpawnActorInCurrentRoomAsync(run, ActorKind.Enemy);
                    break;
                case "Populate every room (NPC/Enemy)":
                    // NPC-biased population for readability in tests.
                    await runService.PopulateActorsEveryRoomAsync(run, npcBias01: 0.6f);
                    break;
                case "Advance 1 turn":
                    await runService.AdvanceTurnAsync(run, "testlab.turn");
                    break;
                case "Advance 5 turns":
                    for (int i = 0; i < 5; i++)
                        await runService.AdvanceTurnAsync(run, "testlab.turn");
                    break;
                case "Advance 20 turns":
                    for (int i = 0; i < 20; i++)
                        await runService.AdvanceTurnAsync(run, "testlab.turn");
                    break;
                case "Back":
                    return;
            }
        }
    }

    private static async Task ShowIntroDialogueAsync(RunState run)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold magenta]The Endless Night[/]").RuleStyle("magenta").Centered());
        AnsiConsole.WriteLine();

        // Generate a unique goal based on the seed
        var goals = new[]
        {
            "[cyan]A cryptic memory pulls at the edges of your mind. Find the House's heart. Something waits there.[/]",
            "[yellow]You remember fragments: a name, a face, the smell of ash. The House holds answers. You must find them.[/]",
            "[magenta]The darkness whispers of a core—a beating, conscious thing beneath the walls. Reach it. Understand it.[/]",
            "[red]They say the House remembers everything. You came here seeking something lost. Find it before the House forgets you too.[/]",
            "[cyan]A ritual, incomplete. A binding, unraveling. The House's heart pulses with stolen time. You must claim it or break free.[/]",
            "[yellow]In your dreams, you saw this place. Now you're here. The way out lies through, not around. Find the heart. Escape.[/]"
        };

        var goalLines = new[]
        {
            "[cyan]Gather artifacts that resonate with power. Solve the House's puzzles. Survive its surprises.[/]",
            "[cyan]Beware the traps hidden in shadow. Each room remembers those who came before. Uncover them all.[/]",
            "[cyan]Campfires provide brief sanctuary. Rest when you can. The House is patient, but the Night is not.[/]",
            "[cyan]Your sanity will fracture. Your morality will be tested. What you become matters as much as what you find.[/]"
        };

        var rng = new Random(run.Seed);
        var selectedGoal = goals[rng.Next(goals.Length)];
        var selectedLines = goalLines.OrderBy(_ => rng.Next()).Take(2).ToList();

        AnsiConsole.MarkupLine(selectedGoal);
        AnsiConsole.WriteLine();
        foreach (var line in selectedLines)
            AnsiConsole.MarkupLine(line);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold magenta]The darkness beckons...[/]");
        ControllerUI.WaitToContinue("Press A or Enter to descend...");
        AnsiConsole.Clear();
    }

    private static async Task InspectSavesAsync(RunService runService, string playerName)
    {
        var runs = await runService.GetRunsAsync(playerName);
        if (runs.Count == 0)
        {
            AnsiConsole.MarkupLine("[cyan]No runs found.[/]");
            Pause();
            return;
        }

        var run = ControllerUI.SelectFromList(
            "Inspect which run?",
            runs,
            r => $"Run {r.RunId} | Seed {r.Seed} | Turn {r.Turn} | Sanity {r.Sanity} | Health {r.Health} | Morality {r.Morality}");

        var room = await runService.GetCurrentRoomAsync(run);
        var inventory = await runService.GetInventoryAsync(run);

        var panel = new Panel($"[cyan]RunId[/]: {run.RunId}\n" +
                              $"[cyan]Seed[/]: {run.Seed}\n" +
                              $"[cyan]Turn[/]: {run.Turn}\n" +
                              $"[cyan]Sanity[/]: {run.Sanity}\n" +
                              $"[cyan]Health[/]: {run.Health}\n" +
                              $"[cyan]Morality[/]: {run.Morality}\n" +
                              $"[cyan]Room[/]: {(room is null ? "<missing>" : room.Name)}\n" +
                              $"[cyan]Items[/]: {inventory.Count}")
        {
            Header = new PanelHeader("[bold cyan]Save Inspect[/]", Justify.Center),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        Pause();
    }

    // NOTE: Story chapter playback and dialogue encounter loop were prototyped earlier,
    // but the referenced RunService APIs have since moved. We'll reintroduce these
    // once the story pipeline is wired back into the current RunService.

    private static IEnumerable<string> GetAvailableActions(RoomInstance room, List<RunInventoryItem> inventory,
        List<WorldObjectInstance> objects)
    {
        yield return "Move";
        yield return "Search room";
        if (objects.Count > 0)
            yield return "Interact";
        yield return "Inventory";
        if (inventory.Count > 0)
            yield return "Use item";
        yield return "Quit";
    }

    private static async Task<bool> MoveMenuAsync(RunService runService, RunState run, RoomInstance room)
    {
        var dirs = room.Exits.Keys.OrderBy(d => d).Select(d => d.ToString()).ToList();
        if (dirs.Count == 0)
        {
            ControllerUI.ShowInfo("No exits. That feels wrong.");
            return false;
        }

        var dirOptions = dirs
            .Select(d => (option: d, description: $"Move {d}"))
            .ToList();
        dirOptions.Add(("🔙 Back", "Return to previous menu"));

        var (pickedDir, _) = ControllerUI.SelectFromMenuWithDescriptions(
            "CHOOSE DIRECTION",
            dirOptions
        );

        if (pickedDir == "🔙 Back")
            return false;

        if (!Enum.TryParse<Direction>(pickedDir, out var dir))
            return false;

        var (ok, error) = await runService.MoveAsync(run, dir);
        if (!ok)
        {
            ControllerUI.ShowError(error ?? "You can't.");
            return false;
        }

        return true;
    }


    private static string GetPercentColor(int value0To100)
    {
        value0To100 = Math.Clamp(value0To100, 0, 100);
        return value0To100 switch
        {
            >= 80 => "green",
            >= 50 => "yellow",
            >= 25 => "orange3",
            >= 1 => "red",
            _ => "grey"
        };
    }

    private static void Pause()
    {
        // Kept only for the debug menus.
        ControllerUI.WaitToContinue();
    }

    private static string EscapeMarkup(string text) => Markup.Escape(text ?? string.Empty);

    private static async Task TalkMenuAsync(RunService runService, RunState run)
    {
        var actors = await runService.GetActorsInCurrentRoomAsync(run);
        if (actors.Count == 0)
        {
            ControllerUI.ShowInfo("No one answers. No one is here.");
            return;
        }

        var actorOptions = actors
            .Select(a => (
                option: $"{a.Kind}: {a.Name}",
                description: a.IsHostile ? "Hostile" : a.IsPacified ? "Pacified" : a.Disposition.ToString()))
            .ToList();
        actorOptions.Add(("🔙 Back", "Return to actions"));

        var (picked, idx) = ControllerUI.SelectFromMenuWithDescriptions("TALK TO", actorOptions);
        if (picked == "🔙 Back")
            return;

        var actor = actors[Math.Clamp(idx, 0, actors.Count - 1)];

        while (true)
        {
            var (node, choices, err) = await runService.GetDialogueAsync(run, actor.Id);
            if (err is not null)
            {
                ControllerUI.ShowError(err);
                return;
            }

            if (node is null)
            {
                ControllerUI.ShowInfo("They have nothing to say.");
                return;
            }

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine($"[bold cyan]{ControllerUI.EscapeMarkup(actor.Name)}[/]");
            if (!string.IsNullOrWhiteSpace(node.Speaker))
                AnsiConsole.MarkupLine($"[grey]{ControllerUI.EscapeMarkup(node.Speaker)}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(ControllerUI.EscapeMarkup(node.Text));
            AnsiConsole.WriteLine();

            if (choices.Count == 0)
            {
                ControllerUI.ShowInfo("The conversation trails off.");
                return;
            }

            var choiceOptions = choices
                .Select(c => (
                    option: c.Text,
                    description: DescribeChoiceEffects(c)))
                .ToList();
            choiceOptions.Add(("🔙 End conversation", "Stop talking"));

            var (pickedChoiceText, choiceIdx) = ControllerUI.SelectFromMenuWithDescriptions("CHOOSE", choiceOptions);
            if (pickedChoiceText == "🔙 End conversation")
                return;

            var chosen = choices[Math.Clamp(choiceIdx, 0, choices.Count - 1)];
            var (ok, msg) = await runService.ChooseDialogueAsync(run, actor.Id, chosen);
            if (!ok)
            {
                ControllerUI.ShowError(msg ?? "That doesn't work.");
                return;
            }

            var deltaSummary = DescribeChoiceEffects(chosen);
            if (!string.IsNullOrWhiteSpace(deltaSummary))
                ControllerUI.ShowSuccess(deltaSummary);
        }
    }

    private static string DescribeChoiceEffects(EndlessNight.Domain.Dialogue.DialogueChoice c)
    {
        var parts = new List<string>();
        if (c.HealthDelta != 0) parts.Add($"Health {(c.HealthDelta > 0 ? "+" : "")} {c.HealthDelta}");
        if (c.SanityDelta != 0) parts.Add($"Sanity {(c.SanityDelta > 0 ? "+" : "")} {c.SanityDelta}");
        if (c.MoralityDelta != 0) parts.Add($"Morality {(c.MoralityDelta > 0 ? "+" : "")} {c.MoralityDelta}");
        if (!string.IsNullOrWhiteSpace(c.GrantItemKey)) parts.Add($"Gain {c.GrantItemKey} x{(c.GrantItemQuantity <= 0 ? 1 : c.GrantItemQuantity)}");
        if (c.PacifyTarget) parts.Add("Pacify");
        if (c.RevealDisposition is not null) parts.Add($"Disposition: {c.RevealDisposition}");
        return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
    }

    private static async Task ResolveEntryTrapsAsync(RunService runService, RunState run)
    {
        while (true)
        {
            var trap = await runService.GetPendingEntryTrapAsync(run);
            if (trap is null)
                return;

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold red]A TRAP TRIGGERS ON ENTRY[/]");
            AnsiConsole.MarkupLine($"[yellow]{ControllerUI.EscapeMarkup(trap.Name)}[/]");
            if (!string.IsNullOrWhiteSpace(trap.Description))
                AnsiConsole.MarkupLine($"[dim]{ControllerUI.EscapeMarkup(trap.Description)}[/]");
            AnsiConsole.WriteLine();

            var options = new List<(string option, string description)>
            {
                ("Disarm", "Spend sanity to carefully disarm it"),
                ("Endure", "Take the hit"),
            };

            var (picked, _) = ControllerUI.SelectFromMenuWithDescriptions("TRAP", options);
            var (ok, msg) = await runService.ResolveEntryTrapAsync(run, trap.Id, disarm: picked == "Disarm");
            if (ok)
                ControllerUI.ShowSuccess(msg);
            else
                ControllerUI.ShowError(msg);
        }
    }

    private static async Task EncounterMenuAsync(RunService runService, RunState run)
    {
        var enemies = await runService.GetEnemiesInCurrentRoomAsync(run);
        if (enemies.Count == 0)
        {
            ControllerUI.ShowInfo("No enemies remain here.");
            return;
        }

        // One enemy at a time for now: pick which enemy to engage.
        var enemyOptions = enemies
            .Select(e => (
                option: $"{e.Name}",
                description: $"Intensity {e.Intensity} | Pacify {(e.PacifyUnlocked ? "Unlocked" : $"Locked ({e.PacifyProgress}%)")}"))
            .ToList();
        enemyOptions.Add(("🔙 Back", "Return"));

        var (pickedEnemy, enemyIdx) = ControllerUI.SelectFromMenuWithDescriptions("ENEMY", enemyOptions);
        if (pickedEnemy == "🔙 Back")
            return;

        var enemy = enemies[Math.Clamp(enemyIdx, 0, enemies.Count - 1)];

        while (true)
        {
            var pacifyUnlocked = await runService.IsPacifyUnlockedAsync(run, enemy.Id);
            var cost = await runService.GetPacifySanityCostAsync(run, enemy);

            var options = new List<(string option, string description)>
            {
                ("Talk", "Try to understand it. Required to unlock Pacify."),
                ("Run", "Retreat. You might get followed."),
                (pacifyUnlocked ? $"Pacify (Sanity -{cost})" : "Pacify (Locked)", pacifyUnlocked ? "Spend sanity to spare it." : "You must talk to unlock this option."),
                ("🔙 Back", "Return")
            };

            var (picked, _) = ControllerUI.SelectFromMenuWithDescriptions($"ENCOUNTER: {enemy.Name}", options);

            if (picked == "🔙 Back")
                return;

            if (picked == "Run")
            {
                // Simple flee: pick the first available exit (deterministic) and move.
                var room = await runService.GetCurrentRoomAsync(run);
                if (room is null || room.Exits.Count == 0)
                {
                    ControllerUI.ShowError("No way out.");
                    continue;
                }

                var dir = room.Exits.Keys.OrderBy(d => d).First();
                var (ok, err) = await runService.MoveAsync(run, dir);
                if (!ok)
                    ControllerUI.ShowError(err ?? "You can't run.");
                else
                    ControllerUI.ShowInfo($"You run {dir}.");

                return;
            }

            if (picked.StartsWith("Pacify", StringComparison.OrdinalIgnoreCase))
            {
                var (ok, msg) = await runService.TryPacifyEnemyAsync(run, enemy.Id);
                if (ok)
                {
                    ControllerUI.ShowSuccess(msg);
                    return;
                }

                ControllerUI.ShowError(msg);
                continue;
            }

            if (picked == "Talk")
            {
                var (ok, msg) = await runService.TalkToEnemyForPacifyProgressAsync(run, enemy.Id);
                if (ok)
                    ControllerUI.ShowInfo(msg);
                else
                    ControllerUI.ShowError(msg);

                // Refresh the local enemy snapshot.
                var refreshed = (await runService.GetEnemiesInCurrentRoomAsync(run)).FirstOrDefault(e => e.Id == enemy.Id);
                if (refreshed is not null)
                    enemy = refreshed;
                else
                    return;

                continue;
            }
        }
    }
}
