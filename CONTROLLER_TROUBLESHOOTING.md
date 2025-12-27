# Controller Troubleshooting Guide

## Issue: Controller not moving cursor/selection

### Solution Implemented
The issue was that Spectre.Console's `SelectionPrompt` doesn't support controller input natively. 

**Changes Made:**
1. Created custom `ControllerMenu.ShowMenu()` that directly handles controller input
2. Replaced all `SelectionPrompt` usages with controller-aware custom menu
3. Updated input polling to 60 FPS (16ms) for smoother response
4. Added input delay (150ms) to prevent too-fast scrolling
5. Implemented hybrid keyboard/controller support

### Testing Your Controller

#### 1. Verify Controller is Detected by Windows
```powershell
# Open Windows Game Controllers settings
joy.cpl
```
- Your Xbox controller should appear in the list
- Click "Properties" to test buttons and sticks
- D-Pad and Left Stick should show movement

#### 2. Run the Game
```powershell
cd C:\Projects\Github\Console\Endless-Night
dotnet run --project EndlessNight\EndlessNight.csproj
```

#### 3. Look for Detection Message
When the game starts, you should see:
```
🎮 Controller detected!
```

If you don't see this message, the controller isn't being detected by XInput.

### Common Issues and Solutions

#### Issue: "Controller detected!" but still not working
**Solution:** The controller is detected but input isn't being read properly.

**Try:**
1. Unplug and replug the controller
2. Close the game completely and restart
3. Try a different USB port
4. Ensure no other applications are using the controller

#### Issue: No "Controller detected!" message
**Possible Causes:**
1. **Controller not XInput compatible**
   - XInput only supports Xbox controllers natively
   - PlayStation controllers need DS4Windows or similar driver

2. **USB connection issue**
   - Try different USB port
   - Check cable (many cheap cables are charge-only)

3. **Bluetooth pairing issue**
   - Re-pair the controller
   - Try wired connection first

4. **Third-party controller**
   - Some third-party controllers don't support XInput
   - Install the manufacturer's driver software

#### Issue: Controller works in other games but not this one
**Solution:** The game uses XInput API which some controllers don't support.

**For PlayStation Controllers:**
1. Install **DS4Windows** (https://ds4-windows.com/)
2. Configure it to emulate Xbox 360 controller
3. Run DS4Windows before starting the game
4. The game should now detect it as Xbox controller

**For Generic Controllers:**
1. Install **x360ce** (Xbox 360 Controller Emulator)
2. Place x360ce in the game's executable folder
3. Configure your controller in x360ce
4. Game should now see it as Xbox controller

### Manual Controller Test

If you want to test if your controller is working at XInput level:

```powershell
# Install a test tool
winget install XInputTest

# Or use Windows built-in game controller testing
joy.cpl
```

### Expected Controller Behavior

#### When Working Correctly:
- **D-Pad Up/Down**: Moves selection highlight up/down in menus
- **Left Stick Up/Down**: Also moves selection highlight
- **A Button (Xbox)**: Selects highlighted option, slight vibration
- **B Button (Xbox)**: Goes back/cancels
- **Navigation vibration**: Very light tap (50ms) when moving between items
- **Selection vibration**: Medium pulse (100ms) when selecting

#### Input Timing:
- **Navigation delay**: 150ms between movements (prevents too-fast scrolling)
- **Polling rate**: 60 FPS (16ms) for smooth response
- **Button presses**: Instant (no delay)

### Debug Mode

To test controller input directly, you can add this temporary test:

```csharp
// Add to Program.cs Main() after controller initialization
if (ControllerUI.IsControllerConnected)
{
    AnsiConsole.MarkupLine("[green]Testing controller...[/]");
    AnsiConsole.MarkupLine("Press buttons to test. Press B to continue.");
    
    var controller = ControllerUI.GetController();
    while (controller != null)
    {
        if (controller.IsDPadUpPressed())
            AnsiConsole.MarkupLine("[cyan]D-Pad UP[/]");
        if (controller.IsDPadDownPressed())
            AnsiConsole.MarkupLine("[cyan]D-Pad DOWN[/]");
        if (controller.IsAButtonPressed())
            AnsiConsole.MarkupLine("[green]A Button[/]");
        if (controller.IsBButtonPressed())
        {
            AnsiConsole.MarkupLine("[red]B Button - Continuing...[/]");
            break;
        }
        Thread.Sleep(16);
    }
}
```

### Controller Support Requirements

#### Supported Controllers:
- ✅ Xbox One Controller
- ✅ Xbox Series X/S Controller
- ✅ Xbox 360 Controller (wired or wireless with adapter)
- ✅ PlayStation DualShock 4 (with DS4Windows)
- ✅ PlayStation DualSense (with DS4Windows)
- ✅ Any XInput-compatible controller

#### Not Directly Supported:
- ❌ DirectInput-only controllers (need x360ce)
- ❌ Generic USB controllers without XInput drivers
- ❌ Nintendo Switch Pro Controller (needs driver)

### Still Not Working?

If you've tried everything and controller still doesn't work:

1. **Use keyboard** - The game fully supports keyboard navigation:
   - Arrow Keys = Navigate
   - Enter = Select
   - Escape = Back

2. **Report the issue** with:
   - Controller model and connection type (USB/Bluetooth)
   - Windows version
   - Does it work in `joy.cpl`?
   - Does it work in other XInput games?
   - Any error messages

3. **Check XInput DLL**:
   ```powershell
   # Check if XInput is installed
   Get-ChildItem C:\Windows\System32\xinput*.dll
   ```
   You should see `xinput1_4.dll` or similar.

### Alternative: Use Keyboard

The keyboard controls work perfectly and are actually very responsive:
- **↑↓ Arrow Keys**: Navigate menus
- **Enter**: Select option
- **Escape**: Go back

Many players prefer keyboard for this type of game!

---

## Technical Details

### Implementation
- **XInput API**: Via SharpDX.XInput NuGet package
- **Polling Rate**: 60 FPS (16ms intervals)
- **Input Delay**: 150ms between navigation inputs
- **Deadzone**: 20% for thumbstick drift prevention
- **Edge Detection**: Prevents button repeat/bouncing

### Files Modified
- `ControllerUI.cs`: Custom menu system
- `ControllerMenu.cs`: Controller input handling
- `ControllerInput.cs`: XInput wrapper
- `Program.cs`: All menu selections use custom system

### Why It Should Work
- Direct XInput API calls (not wrapper-dependent)
- Edge detection for reliable button presses
- 60 FPS polling for responsive input
- Hybrid keyboard support (always works as fallback)
- Tested with Xbox One controller

If your Xbox controller works in Windows settings (`joy.cpl`), it should work in this game.

