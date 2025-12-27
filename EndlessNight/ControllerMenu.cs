using Spectre.Console;
using EndlessNight.Domain;

namespace EndlessNight;

/// <summary>
/// Enhanced menu system with controller and keyboard support
/// </summary>
public static class ControllerMenu
{
    /// <summary>
    /// Display an interactive menu with controller and keyboard support
    /// Returns the selected index
    /// </summary>
    public static int ShowMenu(string title, List<(string option, string description)> items, int startIndex = 0)
    {
        int selectedIndex = startIndex;
        var controller = ControllerUI.GetController();
        bool firstRender = true;
        
        while (true)
        {
            // Render the menu (only clear on first frame)
            RenderMenu(title, items, selectedIndex, firstRender);
            firstRender = false;
            
            // Wait for input (check both controller and keyboard)
            var input = WaitForInput(controller);
            
            switch (input)
            {
                case MenuInput.Up:
                    selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                    controller?.Vibrate(0.1f, 0.1f, 50); // Light feedback
                    break;
                case MenuInput.Down:
                    selectedIndex = (selectedIndex + 1) % items.Count;
                    controller?.Vibrate(0.1f, 0.1f, 50); // Light feedback
                    break;
                case MenuInput.Select:
                    controller?.Vibrate(0.3f, 0.3f, 100); // Selection feedback
                    return selectedIndex;
                case MenuInput.Back:
                    return -1; // Cancelled
            }
        }
    }

    /// <summary>
    /// Display a menu with HUD visible above it
    /// </summary>
    public static int ShowMenuWithHUD(string title, List<(string option, string description)> items, RunState run, RoomInstance room, List<WorldObjectInstance>? visibleObjects = null, int startIndex = 0)
    {
        int selectedIndex = startIndex;
        var controller = ControllerUI.GetController();
        
        while (true)
        {
            // Single clear + full HUD render each frame
            GameHUD.RenderFullHUD(run, room, visibleObjects, clear: true);
            RenderMenuOnly(title, items, selectedIndex);
            
            var input = WaitForInput(controller);
            switch (input)
            {
                case MenuInput.Up:
                    selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
                    controller?.Vibrate(0.1f, 0.1f, 50);
                    break;
                case MenuInput.Down:
                    selectedIndex = (selectedIndex + 1) % items.Count;
                    controller?.Vibrate(0.1f, 0.1f, 50);
                    break;
                case MenuInput.Select:
                    controller?.Vibrate(0.3f, 0.3f, 100);
                    return selectedIndex;
                case MenuInput.Back:
                    return -1;
            }
        }
    }

    private static void RenderMenu(string title, List<(string option, string description)> items, int selectedIndex, bool firstRender = true)
    {
        AnsiConsole.Clear();

        GameHUD.ShowControllerHints(ControllerUI.IsControllerConnected);
        AnsiConsole.WriteLine();

        var rule = new Rule($"[bold white]{ControllerUI.EscapeMarkup(title)}[/]")
            .RuleStyle("cyan");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);

        RenderMenuItems(items, selectedIndex);
    }

    private static void RenderMenuOnly(string title, List<(string option, string description)> items, int selectedIndex)
    {
        GameHUD.ShowControllerHints(ControllerUI.IsControllerConnected);
        AnsiConsole.WriteLine();

        var rule = new Rule($"[bold white]{ControllerUI.EscapeMarkup(title)}[/]")
            .RuleStyle("cyan");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);

        RenderMenuItems(items, selectedIndex);
    }

    private static void RenderMenuItems(List<(string option, string description)> items, int selectedIndex)
    {
        // Compact list: one line per option, and a single description line for the selected item.
        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn(new TableColumn(string.Empty).NoWrap());

        for (int i = 0; i < items.Count; i++)
        {
            var (option, _) = items[i];
            var isSelected = i == selectedIndex;

            var row = isSelected
                ? $"[bold black on cyan] ► {ControllerUI.EscapeMarkup(option)}[/]"
                : $"[cyan]   {ControllerUI.EscapeMarkup(option)}[/]";

            table.AddRow(row);
        }

        AnsiConsole.Write(table);

        var selectedDesc = (selectedIndex >= 0 && selectedIndex < items.Count) ? items[selectedIndex].description : "";
        if (!string.IsNullOrWhiteSpace(selectedDesc))
        {
            AnsiConsole.MarkupLine($"\n[dim]{ControllerUI.EscapeMarkup(selectedDesc)}[/]");
        }

        AnsiConsole.WriteLine();
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
