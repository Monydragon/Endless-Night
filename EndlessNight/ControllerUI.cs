using Spectre.Console;
using EndlessNight.Domain;

namespace EndlessNight;

/// <summary>
/// Handles all UI interactions with controller support via Spectre.Console
/// </summary>
public static class ControllerUI
{
    private static ControllerInput? _controller;

    /// <summary>
    /// Initialize controller support
    /// </summary>
    public static void InitializeController()
    {
        try
        {
            AnsiConsole.MarkupLine("[cyan]Initializing controller support...[/]");
            AnsiConsole.MarkupLine("[dim]Checking all XInput controller slots...[/]");
            
            _controller = new ControllerInput();
            
            if (_controller.IsConnected)
            {
                ShowInfo("🎮 Controller detected and ready!");
                AnsiConsole.MarkupLine("[dim]You can use D-Pad or Left Stick to navigate, A to select, B to go back.[/]");
                Task.Delay(2000).Wait();
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠ No controller detected. Using keyboard only.[/]");
                AnsiConsole.MarkupLine("[dim]Checked all 4 XInput slots - no controller found.[/]");
                AnsiConsole.MarkupLine("[dim]If you have a controller connected, it might not be XInput-compatible.[/]");
                AnsiConsole.MarkupLine("[dim]Keyboard controls: Arrow Keys = Navigate, Enter = Select, Escape = Back[/]");
                Task.Delay(2000).Wait();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Controller initialization error:[/]");
            AnsiConsole.MarkupLine($"[red]{EscapeMarkup(ex.GetType().Name)}: {EscapeMarkup(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[yellow]Falling back to keyboard-only mode.[/]");
            _controller = null;
            Task.Delay(2000).Wait();
        }
    }

    /// <summary>
    /// Check if controller is connected
    /// </summary>
    public static bool IsControllerConnected => _controller?.IsConnected ?? false;

    /// <summary>
    /// Get the controller instance
    /// </summary>
    public static ControllerInput? GetController() => _controller;

    /// <summary>
    /// Select from a list of objects with a custom display function
    /// </summary>
    public static T SelectFromList<T>(string title, List<T> items, Func<T, string> displayFunc) where T : class
    {
        var menuItems = items.Select(item => (option: displayFunc(item), description: "")).ToList();
        var index = ControllerMenu.ShowMenu(title, menuItems);
        
        if (index < 0 || index >= items.Count)
            return items[0]; // Default to first item if cancelled
        
        return items[index];
    }

    /// <summary>
    /// Display a menu and get user selection with keyboard and controller navigation support
    /// </summary>
    public static string SelectFromMenu(string title, List<string> options, string? description = null)
    {
        var menuItems = options.Select(o => (option: o, description: "")).ToList();
        var index = ControllerMenu.ShowMenu(title, menuItems);
        
        if (index < 0 || index >= options.Count)
            return options[0]; // Default to first option if cancelled
        
        return options[index];
    }

    /// <summary>
    /// Display menu with descriptions for each item
    /// </summary>
    public static (string selected, int index) SelectFromMenuWithDescriptions(
        string title,
        List<(string option, string description)> options)
    {
        var selectedIndex = ControllerMenu.ShowMenu(title, options);
        
        if (selectedIndex < 0 || selectedIndex >= options.Count)
            selectedIndex = 0; // Default to first option if cancelled
        
        return (options[selectedIndex].option, selectedIndex);
    }

    /// <summary>
    /// Display menu with HUD and descriptions for each item
    /// </summary>
    public static (string selected, int index) SelectFromMenuWithHUD(
        string title,
        List<(string option, string description)> options,
        RunState run,
        RoomInstance room,
        List<WorldObjectInstance>? visibleObjects = null)
    {
        var selectedIndex = ControllerMenu.ShowMenuWithHUD(title, options, run, room, visibleObjects);
        
        if (selectedIndex < 0 || selectedIndex >= options.Count)
            selectedIndex = 0; // Default to first option if cancelled
        
        return (options[selectedIndex].option, selectedIndex);
    }

    /// <summary>
    /// Display a yes/no confirmation with controller support
    /// </summary>
    public static bool Confirm(string message)
    {
        var choices = new List<(string option, string description)>
        {
            ("Yes", "Confirm action"),
            ("No", "Cancel action")
        };
        
        var (selected, _) = SelectFromMenuWithDescriptions(message, choices);
        return selected == "Yes";
    }

    /// <summary>
    /// Wait for player to continue (Press A/Enter)
    /// </summary>
    public static void WaitToContinue(string? message = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "Press A or Enter to continue...";
        
        AnsiConsole.MarkupLine($"[dim]{message}[/]");
        
        var controller = GetController();
        
        while (true)
        {
            Thread.Sleep(16); // 60 FPS
            
            // Check keyboard
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                    return;
            }
            
            // Check controller
            if (controller != null && controller.IsConnected)
            {
                if (controller.IsAButtonPressed())
                {
                    controller.Vibrate(0.2f, 0.2f, 75);
                    return;
                }
                
                // Also allow B to continue (as alternate)
                if (controller.IsBButtonPressed())
                {
                    controller.Vibrate(0.2f, 0.2f, 75);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Get text input with prompt
    /// </summary>
    public static string GetInput(string prompt)
    {
        return AnsiConsole.Ask<string>($"[bold cyan]➤ {EscapeMarkup(prompt)}[/]").Trim();
    }

    /// <summary>
    /// Display a message and wait for key press (with controller support)
    /// </summary>
    public static void ShowMessage(string message, string? title = null)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            AnsiConsole.MarkupLine($"[bold cyan]{EscapeMarkup(title)}[/]");
        }
        AnsiConsole.MarkupLine($"[cyan]{EscapeMarkup(message ?? "")}[/]");
        WaitToContinue();
    }

    /// <summary>
    /// Display error message
    /// </summary>
    public static void ShowError(string? message)
    {
        AnsiConsole.MarkupLine($"[red]✗ {EscapeMarkup(message ?? "An error occurred.")}[/]");
        
        // Controller feedback: vibrate on error
        if (IsControllerConnected)
        {
            _controller?.Vibrate(0.7f, 0.3f, 300);
        }

        WaitToContinue();
    }

    /// <summary>
    /// Display success message
    /// </summary>
    public static void ShowSuccess(string? message)
    {
        AnsiConsole.MarkupLine($"[green]✓ {EscapeMarkup(message ?? "Success.")}[/]");
        
        // Controller feedback: light vibrate on success
        if (IsControllerConnected)
        {
            _controller?.Vibrate(0.3f, 0.3f, 150);
        }

        WaitToContinue();
    }

    /// <summary>
    /// Display info message
    /// </summary>
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[cyan]{EscapeMarkup(message)}[/]");
    }

    /// <summary>
    /// Display a structured table
    /// </summary>
    public static void ShowTable(string title, List<(string column1, string column2)> items, 
        string header1 = "Item", string header2 = "Value")
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine($"[bold cyan]{EscapeMarkup(title)}[/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Centered()
            .AddColumn($"[cyan]{header1}[/]")
            .AddColumn($"[cyan]{header2}[/]");

        foreach (var (col1, col2) in items)
        {
            table.AddRow(col1, col2);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[dim]Press Enter to continue...[/]");
        Console.ReadLine();
    }

    /// <summary>
    /// Display a panel with information
    /// </summary>
    public static void ShowPanel(string content, string? title = null)
    {
        var panel = new Panel(new Markup(EscapeMarkup(content)))
        {
            Header = !string.IsNullOrWhiteSpace(title) 
                ? new PanelHeader($"[bold cyan]{EscapeMarkup(title)}[/]", Justify.Center)
                : null,
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan),
            Padding = new Padding(1, 1, 1, 1)
        };

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Display a rule/separator
    /// </summary>
    public static void ShowRule(string title)
    {
        AnsiConsole.Write(
            new Rule($"[bold cyan]{EscapeMarkup(title)}[/]")
                .RuleStyle("cyan")
                .Centered());
    }

    /// <summary>
    /// Display loading/spinner animation
    /// </summary>
    public static void ShowLoading(string message, Action action)
    {
        AnsiConsole.Status()
            .Start($"[cyan]{EscapeMarkup(message)}[/]", ctx =>
            {
                action();
            });
    }

    /// <summary>
    /// Escape markup characters in text to prevent parsing errors
    /// </summary>
    public static string EscapeMarkup(string? text)
    {
        return Markup.Escape(text ?? string.Empty);
    }
}

