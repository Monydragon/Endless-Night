using EndlessNight.Domain;
using EndlessNight.Domain.Dialogue;
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
            using var db = SqliteDbContextFactory.Create(configuration);

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
            if (!SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
            {
                AnsiConsole.MarkupLine("[red]Automatic reset failed.[/] You can manually delete the DB file or set env var [grey]Sqlite__ResetOnModelMismatch=true[/].");
                return 1;
            }

            using var db2 = SqliteDbContextFactory.Create(sqliteConnectionString, resetOnModelMismatch: false);
            await new Seeder(db2).EnsureSeededAsync();
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

            if (choice == "Move")
            {
                await MoveMenuAsync(runService, run, room);
                continue;
            }
        }
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
                "Inspect Saves (Debug)", "Reset DB (Debug)", 
                "Recreate DB (Fix tables)", "Quit" 
            };
            
            var choice = ControllerUI.SelectFromMenu("Main Menu", choices);

            if (choice == "Quit")
                return null;

            if (choice == "Reset DB (Debug)")
            {
                var confirm = ControllerUI.Confirm("This will [red]delete[/] your SQLite DB file and all saves. Continue?");
                if (!confirm)
                    continue;

                if (!SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
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

                if (!SqliteDbContextFactory.TryResetDatabase(sqliteConnectionString))
                {
                    AnsiConsole.MarkupLine("[red]Could not reset the DB.[/] Only simple 'Data Source=...' connection strings are supported.");
                    Pause();
                    continue;
                }

                using var db = SqliteDbContextFactory.Create(sqliteConnectionString, resetOnModelMismatch: false);
                await new Seeder(db).EnsureSeededAsync();
                AnsiConsole.MarkupLine("[green]Database recreated and seeded.[/]");
                Pause();
                continue;
            }

            if (choice == "New Game")
            {
                var run = await runService.CreateNewRunAsync(playerName);
                await ShowIntroDialogueAsync(run);
                return run;
            }

            if (choice == "New Game (Seeded)")
            {
                var seedText = AnsiConsole.Ask<string>("Enter a seed ([grey]int[/]) or leave blank for random:").Trim();
                int? seed = null;
                if (!string.IsNullOrWhiteSpace(seedText) && int.TryParse(seedText, out var parsed))
                    seed = parsed;

                var run = await runService.CreateNewRunAsync(playerName, seed);
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
                var run = await runService.CreateNewRunAsync(playerName);
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

    private static async Task TryPlayStoryChaptersAsync(RunService runService, RunState run)
    {
        var (started, chapterTitle) = await runService.TryStartNextStoryChapterAsync(run);
        if (!started)
            return;

        if (!string.IsNullOrWhiteSpace(chapterTitle))
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[bold]{EscapeMarkup(chapterTitle)}[/]").RuleStyle("grey").Centered());
        }

        while (true)
        {
            var (node, choices, error) = await runService.GetActiveStoryDialogueAsync(run);
            if (error is not null)
            {
                AnsiConsole.MarkupLine($"[red]{EscapeMarkup(error)}[/]");
                return;
            }

            if (node is null)
                return;

            AnsiConsole.MarkupLine($"\n[grey]{EscapeMarkup(node.Text)}[/]");

            if (choices.Count == 0)
                return;

            var picked = ControllerUI.SelectFromList(
                "Choose",
                choices,
                c => c.Text);

            var (ok, chapterEnded, message) = await runService.ChooseActiveStoryDialogueAsync(run, picked.Id);
            if (!ok)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    AnsiConsole.MarkupLine($"[red]{EscapeMarkup(message)}[/]");
                return;
            }

            if (chapterEnded)
                return;
        }
    }

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

    private static async Task RunEncounterAsync(RunService runService, RunState run, ActorInstance actor)
    {
        while (true)
        {
            var (node, choices, error) = await runService.GetDialogueAsync(run, actor.Id);
            if (error is not null)
            {
                AnsiConsole.MarkupLine($"[red]{EscapeMarkup(error)}[/]");
                return;
            }

            if (node is null)
                return;

            AnsiConsole.Clear();

            // Procedural 'whisper' line (non-LLM), if present.
            var whisper = await runService.GetLatestProceduralDialogueForActorAsync(run, actor.Id);
            if (!string.IsNullOrWhiteSpace(whisper))
            {
                AnsiConsole.MarkupLine($"[italic orange3]{EscapeMarkup(whisper)}[/]");
                AnsiConsole.WriteLine();
            }

            AnsiConsole.Write(new Rule($"[bold]{EscapeMarkup(node.Speaker)}[/]").RuleStyle("grey").Centered());
            AnsiConsole.MarkupLine($"{EscapeMarkup(node.Text)}");
            AnsiConsole.WriteLine();

            if (choices.Count == 0)
                return;

            var picked = ControllerUI.SelectFromList(
                "Choose",
                choices,
                c => c.Text);

            var (ok, message) = await runService.ChooseDialogueAsync(run, actor.Id, picked);
            if (!ok)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    AnsiConsole.MarkupLine($"[red]{EscapeMarkup(message)}[/]");
                return;
            }

            // If the dialogue state is removed, the encounter is done.
            var (node2, choices2, error2) = await runService.GetDialogueAsync(run, actor.Id);
            if (error2 is not null)
                return;

            if (node2 is null || choices2.Count == 0)
                return;
        }
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
}
