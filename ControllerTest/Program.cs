using EndlessNight;
using Spectre.Console;
using SharpDX.XInput;

// Simple controller test program
AnsiConsole.MarkupLine("[bold yellow]Controller Test Program[/]");
AnsiConsole.MarkupLine("[grey]This will test your Xbox controller input directly[/]");
AnsiConsole.WriteLine();

// First, manually check each XInput slot
AnsiConsole.MarkupLine("[cyan]Checking XInput controller slots directly:[/]");
bool foundController = false;
int foundSlot = -1;

for (int i = 0; i < 4; i++)
{
    var userIndex = (UserIndex)i;
    var testController = new Controller(userIndex);
    var isConnected = testController.IsConnected;
    
    var status = isConnected ? "[green]✓ CONNECTED[/]" : "[grey]✗ Not connected[/]";
    AnsiConsole.MarkupLine($"  Slot {i + 1} ({userIndex}): {status}");
    
    if (isConnected && !foundController)
    {
        foundController = true;
        foundSlot = i;
    }
}

AnsiConsole.WriteLine();

if (!foundController)
{
    AnsiConsole.MarkupLine("[red]❌ No XInput controllers detected in any slot![/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[yellow]Possible reasons:[/]");
    AnsiConsole.MarkupLine("  1. Controller not plugged in");
    AnsiConsole.MarkupLine("  2. Controller is not XInput-compatible");
    AnsiConsole.MarkupLine("  3. Controller drivers not installed");
    AnsiConsole.MarkupLine("  4. Another application has exclusive access");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[cyan]What type of controller do you have?[/]");
    AnsiConsole.MarkupLine("  • Xbox controllers: Native XInput support ✓");
    AnsiConsole.MarkupLine("  • PlayStation controllers: Need DS4Windows");
    AnsiConsole.MarkupLine("  • Generic controllers: Need x360ce");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Press Enter to exit...[/]");
    Console.ReadLine();
    return 1;
}

AnsiConsole.MarkupLine($"[green]✓ Found controller in slot {foundSlot + 1}![/]");
AnsiConsole.WriteLine();

// Now test with our wrapper
AnsiConsole.MarkupLine("[cyan]Testing with ControllerInput wrapper:[/]");
ControllerUI.InitializeController();
AnsiConsole.WriteLine();

if (!ControllerUI.IsControllerConnected)
{
    AnsiConsole.MarkupLine("[red]No controller detected![/]");
    AnsiConsole.MarkupLine("[yellow]Please make sure your Xbox controller is plugged in.[/]");
    AnsiConsole.MarkupLine("[dim]Press Enter to exit...[/]");
    Console.ReadLine();
    return 1;
}

AnsiConsole.MarkupLine("[green]✓ Controller connected![/]");
AnsiConsole.MarkupLine("[cyan]Now testing input...[/]");
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold]Press buttons to test (press B or Escape to exit):[/]");
AnsiConsole.WriteLine();

var controller = ControllerUI.GetController();
if (controller == null)
{
    AnsiConsole.MarkupLine("[red]Error: Controller object is null![/]");
    return 1;
}

var testCount = 0;
while (testCount < 100) // Prevent infinite loop
{
    Thread.Sleep(16); // 60 FPS
    
    // Test D-Pad
    if (controller.IsDPadUpPressed())
    {
        AnsiConsole.MarkupLine($"[cyan]{DateTime.Now:HH:mm:ss.fff} - D-Pad UP pressed[/]");
        testCount++;
    }
    if (controller.IsDPadDownPressed())
    {
        AnsiConsole.MarkupLine($"[cyan]{DateTime.Now:HH:mm:ss.fff} - D-Pad DOWN pressed[/]");
        testCount++;
    }
    if (controller.IsDPadLeftPressed())
    {
        AnsiConsole.MarkupLine($"[cyan]{DateTime.Now:HH:mm:ss.fff} - D-Pad LEFT pressed[/]");
        testCount++;
    }
    if (controller.IsDPadRightPressed())
    {
        AnsiConsole.MarkupLine($"[cyan]{DateTime.Now:HH:mm:ss.fff} - D-Pad RIGHT pressed[/]");
        testCount++;
    }
    
    // Test buttons
    if (controller.IsAButtonPressed())
    {
        AnsiConsole.MarkupLine($"[green]{DateTime.Now:HH:mm:ss.fff} - A Button pressed[/]");
        controller.Vibrate(0.3f, 0.3f, 100);
        testCount++;
    }
    if (controller.IsBButtonPressed())
    {
        AnsiConsole.MarkupLine($"[red]{DateTime.Now:HH:mm:ss.fff} - B Button pressed - Exiting...[/]");
        break;
    }
    if (controller.IsXButtonPressed())
    {
        AnsiConsole.MarkupLine($"[yellow]{DateTime.Now:HH:mm:ss.fff} - X Button pressed[/]");
        testCount++;
    }
    if (controller.IsYButtonPressed())
    {
        AnsiConsole.MarkupLine($"[magenta]{DateTime.Now:HH:mm:ss.fff} - Y Button pressed[/]");
        testCount++;
    }
    
    // Test left thumbstick
    var (x, y) = controller.GetLeftThumbstick();
    if (Math.Abs(x) > 0.01f || Math.Abs(y) > 0.01f)
    {
        AnsiConsole.MarkupLine($"[dim]{DateTime.Now:HH:mm:ss.fff} - Left Stick: X={x:F2}, Y={y:F2}[/]");
    }
    
    // Check keyboard for exit
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
        {
            AnsiConsole.MarkupLine($"[red]{DateTime.Now:HH:mm:ss.fff} - Escape pressed - Exiting...[/]");
            break;
        }
    }
}

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[green]Test complete![/]");
AnsiConsole.MarkupLine($"[dim]Total inputs detected: {testCount}[/]");
AnsiConsole.MarkupLine("[dim]Press Enter to exit...[/]");
Console.ReadLine();

return 0;

