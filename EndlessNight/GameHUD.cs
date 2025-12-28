using EndlessNight.Domain;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace EndlessNight;

/// <summary>
/// Dynamic and responsive HUD system for displaying game state
/// </summary>
public static class GameHUD
{
    public static bool DebugMode { get; set; } = false;

    /// <summary>
    /// Render the complete HUD with all player and room information
    /// </summary>
    public static void RenderFullHUD(RunState run, RoomInstance room, List<WorldObjectInstance>? visibleObjects = null, bool clear = false)
    {
        if (clear)
            AnsiConsole.Clear();
        
        // Title (simple centered text, no box)
        RenderTitle();
        
        // Single row: Health | Sanity | Morality
        RenderStatsRow(run);
        
        // Current Room panel with items inside (compact)
        RenderRoomPanel(room, visibleObjects);
        
        // Exits on same line as atmosphere
        RenderExitsAndAtmosphere(room, run);
    }

    private static void RenderTitle()
    {
        // Use Spectre's Rule instead of manually drawing ───── (prevents broken lines in some terminals)
        AnsiConsole.Write(new Rule("[bold white]E N D L E S S   N I G H T[/]")
            .RuleStyle("cyan")
            .Centered());
    }

    private static void RenderStatsRow(RunState run)
    {
        var healthColor = GetHealthColor(run.Health);
        var sanityColor = GetSanityColor(run.Sanity);
        var moralityColor = GetMoralityColor(run.Morality);
        var moralitySymbol = GetMoralitySymbol(run.Morality);

        var width = ConsoleConfig.GetConsoleWidth();
        var barWidth = Math.Clamp(width / 18, 8, 18);

        var healthBar = CreateBar(run.Health, 100, barWidth, healthColor);
        var sanityBar = CreateBar(run.Sanity, 100, barWidth, sanityColor);

        // Keep it one line. Do NOT shorten labels.
        AnsiConsole.MarkupLine(
            $"[bold red]HP[/]: {healthBar} [bold {healthColor}]{run.Health}[/]  " +
            $"[bold magenta]SANITY[/]: {sanityBar} [bold {sanityColor}]{run.Sanity}[/]  " +
            $"[bold yellow]MORALITY[/]: [bold {moralityColor}]{moralitySymbol}{run.Morality}[/] {GetMoralityDescription(run.Morality)}  " +
            $"[orange3]Turn:[/] [bold white]{run.Turn}[/]"
        );
    }

    private static void RenderRoomPanel(RoomInstance room, List<WorldObjectInstance>? visibleObjects)
    {
        // Panels/borders can tear in IDE consoles. Render a clean, borderless section instead.
        var roomRule = new Rule("[cyan]ROOM[/]")
            .RuleStyle("cyan");
        roomRule.Justification = Justify.Left;
        AnsiConsole.Write(roomRule);

        var roomColor = GetDangerColor(room.DangerRating);
        AnsiConsole.MarkupLine($"[bold {roomColor}]{ControllerUI.EscapeMarkup(room.Name)}[/]");
        AnsiConsole.MarkupLine($"[orange3]{ControllerUI.EscapeMarkup(room.Description)}[/]");

        if (DebugMode)
        {
            var dangerBar = CreateBar(room.DangerRating, 5, 5, roomColor);
            AnsiConsole.MarkupLine(
                $"[orange3]Pos[/]: [white]({room.X},{room.Y})[/]  " +
                $"[orange3]Danger[/]: {dangerBar} [bold {roomColor}]{room.DangerRating}[/]/[bold white]5[/]  " +
                $"[orange3]Searched[/]: {(room.HasBeenSearched ? "[bold green]Y[/]" : "[bold yellow]N[/]")}" 
            );
        }

        if (visibleObjects != null && visibleObjects.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]Items[/]:");
            foreach (var obj in visibleObjects)
                AnsiConsole.MarkupLine($"  {GetObjectIcon(obj.Kind)} [white]{ControllerUI.EscapeMarkup(obj.Name)}[/] [orange3]({GetObjectStatus(obj)})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[orange3]Items[/]: [white]none[/]");
        }

        AnsiConsole.WriteLine();
    }

    private static void RenderExitsAndAtmosphere(RoomInstance room, RunState run)
    {
        var (text, color) = run.Sanity switch
        {
            >= 80 => ("You feel oddly confident. LIKE a seasoned adventurer.", "green"),
            >= 60 => ("You have a strange urge to TYPE: LOOK. The darkness waits.", "yellow"),
            >= 40 => ("A whisper: \"It is pitch black. You are likely to be eaten by a grue.\"", "cyan"),
            >= 20 => ("The parser stutters: TAKE LAMP. OPEN DOOR. The grue is near.", "magenta"),
            _ => ("IT IS PITCH BLACK. You are likely to be eaten by a grue.", "red")
        };

        var exitsText = room.Exits.Count == 0
            ? "[red]Exits[/]: [white]none[/]"
            : $"[white]Exits[/]: {string.Join(" | ", room.Exits.Keys.Select(d => $"[bold cyan]{d}[/]"))}";

        AnsiConsole.MarkupLine($"{exitsText}  [italic {color}]{text}[/]");
    }

    private static string CreateBar(int current, int max, int width, string color)
    {
        var percentage = (float)current / max;
        var filledWidth = (int)(width * percentage);
        var emptyWidth = width - filledWidth;
        var filled = new string('█', Math.Max(0, filledWidth));
        var empty = new string('░', Math.Max(0, emptyWidth));
        return $"[{color}]{filled}[/][orange3]{empty}[/]";
    }


    // Helper methods
    private static string GetHealthColor(int health) => health switch
    {
        >= 75 => "green",
        >= 50 => "yellow",
        >= 25 => "orange3",
        _ => "red"
    };

    private static string GetSanityColor(int sanity) => sanity switch
    {
        >= 75 => "green",
        >= 50 => "cyan",
        >= 25 => "magenta",
        _ => "red"
    };

    private static string GetMoralityColor(int morality) => morality switch
    {
        > 0 => "green",
        < 0 => "red",
        _ => "grey"
    };

    private static string GetMoralitySymbol(int morality) => morality switch
    {
        > 0 => "↑",
        < 0 => "↓",
        _ => "→"
    };

    private static string GetMoralityDescription(int morality) => morality switch
    {
        >= 50 => "[green]Saint[/]",
        >= 20 => "[green]Good[/]",
        > 0 => "[yellow]Kind[/]",
        0 => "[grey]Neutral[/]",
        > -20 => "[orange3]Harsh[/]",
        > -50 => "[red]Evil[/]",
        _ => "[red]Monster[/]"
    };

    private static string GetDangerColor(int danger) => danger switch
    {
        >= 4 => "red",
        >= 3 => "orange3",
        >= 2 => "yellow",
        >= 1 => "cyan",
        _ => "green"
    };

    private static string GetObjectIcon(WorldObjectKind kind) => kind switch
    {
        WorldObjectKind.Chest => "[yellow]■[/]",
        WorldObjectKind.Trap => "[red]![/]",
        WorldObjectKind.PuzzleGate => "[magenta]#[/]",
        WorldObjectKind.Campfire => "[orange3]*[/]",
        WorldObjectKind.GroundItem => "[white]•[/]",
        _ => "[white]•[/]"
    };

    private static string GetObjectStatus(WorldObjectInstance obj) => obj.Kind switch
    {
        WorldObjectKind.Chest => obj.IsOpened ? "[green]Opened[/]" : "[yellow]Locked[/]",
        WorldObjectKind.Trap => obj.IsDisarmed ? "[green]Disarmed[/]" : "[red]Armed[/]",
        WorldObjectKind.PuzzleGate => obj.IsSolved ? "[green]Open[/]" : "[yellow]Locked[/]",
        WorldObjectKind.Campfire => "[cyan]Available[/]",
        _ => "[white]Usable[/]"
    };

    private static string GetObjectTypeColor(WorldObjectKind kind) => kind switch
    {
        WorldObjectKind.Chest => "yellow",
        WorldObjectKind.Trap => "red",
        WorldObjectKind.PuzzleGate => "magenta",
        WorldObjectKind.Campfire => "orange3",
        _ => "white"
    };

    private static string GetDirectionArrow(Direction direction) => direction switch
    {
        Direction.North => "↑",
        Direction.South => "↓",
        Direction.East => "→",
        Direction.West => "←",
        _ => ""
    };

    /// <summary>
    /// Show controller hints at the bottom of the screen
    /// </summary>
    public static void ShowControllerHints(bool controllerConnected)
    {
        // Intentionally no-op.
        // IDE/scrolling consoles can't truly clear and repeated footers look like duplicated UI.
        // Contextual prompts are shown within menus and interactions instead.
    }
}
