using Spectre.Console;
using Spectre.Console.Rendering;
using EndlessNight.Domain;
using EndlessNight.Services.Feedback;
using EndlessNight.Services.Ui;

namespace EndlessNight;

/// <summary>
/// Enhanced menu system with controller and keyboard support
/// </summary>
public static class ControllerMenu
{
    // Central feedback instance (simple, config-driven). Set once in Program.
    public static FeedbackService? Feedback { get; set; }
    public static Func<RunState?>? RunStateProvider { get; set; }

    private static bool _liveActive;

    private static readonly MenuRenderMode DefaultRenderMode = MenuRenderer.DetectDefaultMode();

    /// <summary>
    /// Display an interactive menu with controller and keyboard support
    /// Returns the selected index
    /// </summary>
    public static int ShowMenu(string title, List<(string option, string description)> items, int startIndex = 0)
    {
        if (_liveActive)
            return -1;

        int selectedIndex = startIndex;
        var controller = ControllerUI.GetController();

        _liveActive = true;
        try
        {
            return MenuRenderer.RunMenuLoop(
                DefaultRenderMode,
                build: () => BuildMenuRenderable(title, items, selectedIndex, withHud: false, run: null, room: null, visibleObjects: null),
                pollStep: () =>
                {
                    var input = WaitForInput(controller);
                    var run = RunStateProvider?.Invoke();

                    switch (input)
                    {
                        case MenuInput.Up:
                            selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                            controller?.Vibrate(0.1f, 0.1f, 50);
                            Feedback?.MenuMove(run);
                            return MenuPollResult.ContinueDirty;
                        case MenuInput.Down:
                            selectedIndex = (selectedIndex + 1) % items.Count;
                            controller?.Vibrate(0.1f, 0.1f, 50);
                            Feedback?.MenuMove(run);
                            return MenuPollResult.ContinueDirty;
                        case MenuInput.Select:
                            controller?.Vibrate(0.3f, 0.3f, 100);
                            Feedback?.MenuSelect(run);
                            return MenuPollResult.Exit(selectedIndex);
                        case MenuInput.Back:
                            Feedback?.MenuBack(run);
                            return MenuPollResult.Exit(-1);
                        default:
                            return MenuPollResult.ContinueClean;
                    }
                });
        }
        finally
        {
            _liveActive = false;
        }
    }

    /// <summary>
    /// Display a menu with HUD visible above it
    /// </summary>
    public static int ShowMenuWithHUD(string title, List<(string option, string description)> items, RunState run, RoomInstance room, List<WorldObjectInstance>? visibleObjects = null, int startIndex = 0)
    {
        if (_liveActive)
            return -1;

        int selectedIndex = startIndex;
        var controller = ControllerUI.GetController();

        _liveActive = true;
        try
        {
            return MenuRenderer.RunMenuLoop(
                DefaultRenderMode,
                build: () => BuildMenuRenderable(title, items, selectedIndex, withHud: true, run: run, room: room, visibleObjects: visibleObjects),
                pollStep: () =>
                {
                    var input = WaitForInput(controller);
                    switch (input)
                    {
                        case MenuInput.Up:
                            selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                            controller?.Vibrate(0.1f, 0.1f, 50);
                            Feedback?.MenuMove(run);
                            return MenuPollResult.ContinueDirty;
                        case MenuInput.Down:
                            selectedIndex = (selectedIndex + 1) % items.Count;
                            controller?.Vibrate(0.1f, 0.1f, 50);
                            Feedback?.MenuMove(run);
                            return MenuPollResult.ContinueDirty;
                        case MenuInput.Select:
                            controller?.Vibrate(0.3f, 0.3f, 100);
                            Feedback?.MenuSelect(run);
                            return MenuPollResult.Exit(selectedIndex);
                        case MenuInput.Back:
                            Feedback?.MenuBack(run);
                            return MenuPollResult.Exit(-1);
                        default:
                            return MenuPollResult.ContinueClean;
                    }
                });
        }
        finally
        {
            _liveActive = false;
        }
    }

    private static IRenderable BuildMenuRenderable(
        string title,
        List<(string option, string description)> items,
        int selectedIndex,
        bool withHud,
        RunState? run,
        RoomInstance? room,
        List<WorldObjectInstance>? visibleObjects)
    {
        var rows = new List<IRenderable>();

        // Hints are intentionally omitted from non-HUD menus to avoid duplicated header lines
        // in scrolling/IDE consoles where previous output remains visible.

        if (withHud && run is not null && room is not null)
        {
            // Reuse the existing HUD render methods by building the same layout as text.
            // We keep it simple: render HUD into the live surface by calling the methods that write markup.
            // Since those methods write directly to the console, we instead mirror the same information using renderables.

            rows.Add(new Rule("[bold white]E N D L E S S   N I G H T[/]").RuleStyle("cyan").Centered());

            var healthColor = run.Health switch { >= 75 => "green", >= 50 => "yellow", >= 25 => "orange3", _ => "red" };
            var sanityColor = run.Sanity switch { >= 75 => "green", >= 50 => "cyan", >= 25 => "magenta", _ => "red" };
            var moralityColor = run.Morality switch { > 0 => "green", < 0 => "red", _ => "grey" };
            var moralitySymbol = run.Morality switch { > 0 => "↑", < 0 => "↓", _ => "→" };

            rows.Add(new Markup($"[bold red]HP[/]: [bold {healthColor}]{run.Health}[/]  [bold magenta]SANITY[/]: [bold {sanityColor}]{run.Sanity}[/]  [bold yellow]MORALITY[/]: [bold {moralityColor}]{moralitySymbol}{run.Morality}[/]  [orange3]Turn:[/] [bold white]{run.Turn}[/]"));

            var roomColor = room.DangerRating switch { >= 4 => "red", >= 3 => "orange3", >= 2 => "yellow", >= 1 => "cyan", _ => "green" };
            rows.Add(new Rule("[cyan]ROOM[/]").RuleStyle("cyan").LeftJustified());
            rows.Add(new Markup($"[bold {roomColor}]{ControllerUI.EscapeMarkup(room.Name)}[/]"));
            rows.Add(new Markup($"[orange3]{ControllerUI.EscapeMarkup(room.Description)}[/]"));

            var exits = room.Exits.Count == 0
                ? "[red]Exits[/]: [white]none[/]"
                : $"[white]Exits[/]: {string.Join(" | ", room.Exits.Keys.Select(d => $"[bold cyan]{d}[/]"))}";
            rows.Add(new Markup(exits));

            // Show debug info when enabled (parity with GameHUD).
            if (GameHUD.DebugMode)
            {
                rows.Add(new Markup($"[dim]DEBUG:[/] Pos {room.X},{room.Y} | Danger {room.DangerRating} | Searched {(room.HasBeenSearched ? "yes" : "no")}"));
            }

            rows.Add(new Text(""));
        }

        rows.Add(new Text(""));
        rows.Add(new Rule($"[bold white]{ControllerUI.EscapeMarkup(title)}[/]").RuleStyle("cyan").LeftJustified());

        var table = new Table().Border(TableBorder.None).AddColumn(new TableColumn(string.Empty).NoWrap());
        for (int i = 0; i < items.Count; i++)
        {
            var (option, _) = items[i];
            var isSelected = i == selectedIndex;
            var row = isSelected
                ? $"[bold black on cyan] ► {ControllerUI.EscapeMarkup(option)}[/]"
                : $"[cyan]   {ControllerUI.EscapeMarkup(option)}[/]";
            table.AddRow(row);
        }
        rows.Add(table);

        var selectedDesc = (selectedIndex >= 0 && selectedIndex < items.Count) ? items[selectedIndex].description : "";
        if (!string.IsNullOrWhiteSpace(selectedDesc))
            rows.Add(new Markup($"[dim]{ControllerUI.EscapeMarkup(selectedDesc)}[/]"));

        return new Rows(rows);
    }

    private static MenuInput WaitForInput(ControllerInput? controller)
    {
        var lastInput = DateTime.UtcNow;
        const int inputDelayMs = 150; // Delay between inputs to prevent too-fast scrolling
        
        while (true)
        {
            Thread.Sleep(16); // ~60 FPS polling
            
            var now = DateTime.UtcNow;
            var timeSinceLastInput = (now - lastInput).TotalMilliseconds;
            
            // Check controller input if available
            if (controller != null && controller.IsConnected)
            {
                // Check buttons (no delay needed for selection/back)
                if (controller.IsAButtonPressed())
                    return MenuInput.Select;
                
                if (controller.IsBButtonPressed())
                    return MenuInput.Back;
                
                // Check directional input with delay
                if (timeSinceLastInput >= inputDelayMs)
                {
                    // Check D-pad
                    if (controller.IsDPadUpPressed())
                    {
                        lastInput = now;
                        return MenuInput.Up;
                    }
                    
                    if (controller.IsDPadDownPressed())
                    {
                        lastInput = now;
                        return MenuInput.Down;
                    }
                    
                    // Check left thumbstick
                    var (_, y) = controller.GetLeftThumbstick();
                    if (y > 0.5f)
                    {
                        lastInput = now;
                        return MenuInput.Up;
                    }
                    if (y < -0.5f)
                    {
                        lastInput = now;
                        return MenuInput.Down;
                    }
                }
            }
            
            // Always check keyboard input (for hybrid support)
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                lastInput = now;
                return key.Key switch
                {
                    ConsoleKey.UpArrow => MenuInput.Up,
                    ConsoleKey.DownArrow => MenuInput.Down,
                    ConsoleKey.Enter => MenuInput.Select,
                    ConsoleKey.Escape => MenuInput.Back,
                    _ => MenuInput.None
                };
            }
        }
    }

    private enum MenuInput
    {
        None,
        Up,
        Down,
        Select,
        Back
    }
}
