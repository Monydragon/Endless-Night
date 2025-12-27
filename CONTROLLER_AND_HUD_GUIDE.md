# Controller Support & Dynamic HUD System

## Overview
Endless Night now features full controller support and a dynamic, responsive HUD system that displays player stats, room details, and environmental information in real-time.

## Controller Support

### Supported Controllers
- **Xbox Controllers** (Xbox One, Xbox Series X/S, Xbox 360)
- **PlayStation Controllers** (DualShock 4, DualSense)
- Any XInput-compatible controller

### Controller Detection
The game automatically detects connected controllers at startup. If a controller is found, you'll see:
```
🎮 Controller detected!
```

### Controls

#### Menu Navigation
- **D-Pad Up/Down** or **Left Stick Up/Down**: Navigate menu options
- **A Button (Xbox) / X Button (PlayStation)**: Select/Confirm
- **B Button (Xbox) / Circle Button (PlayStation)**: Back/Cancel

#### Gameplay Controls
- **D-Pad/Left Stick**: Navigate through menus and select actions
- **A/X**: Confirm selections, interact with objects
- **B/Circle**: Go back or cancel

### Haptic Feedback
The controller provides vibration feedback for:
- **Success Actions** (light vibration, 150ms): Item pickup, puzzle solved, rest completed
- **Error Actions** (strong vibration, 300ms): Invalid action, trap triggered, locked chest
- **Navigation** (very light vibration, 50ms): Moving between menu items
- **Selection** (medium vibration, 100ms): Confirming a choice

### Hybrid Input
You can seamlessly switch between controller and keyboard at any time. The game supports:
- **Keyboard**: Arrow keys for navigation, Enter to select, Escape to cancel
- **Controller**: D-Pad/Left Stick for navigation, A/X to select, B/Circle to cancel

## Dynamic HUD System

### HUD Components

#### 1. Title Banner
```
╔═══════════════════════════════════════════════════════════════════════════╗
║                          E N D L E S S   N I G H T                          ║
╚═══════════════════════════════════════════════════════════════════════════╝
```

#### 2. Player Stats Panel
Displays real-time information about the player:
- **❤ Health**: Visual bar (0-100)
  - Green: 75-100 (healthy)
  - Yellow: 50-74 (wounded)
  - Orange: 25-49 (critical)
  - Red: 0-24 (near death)

- **⚡ Sanity**: Visual bar (0-100)
  - Green: 75-100 (stable)
  - Cyan: 50-74 (anxious)
  - Magenta: 25-49 (disturbed)
  - Red: 0-24 (breaking)

- **⚖ Morality**: Numeric value with direction indicator
  - ↑ Green: Positive morality (good actions)
  - → Grey: Neutral (0)
  - ↓ Red: Negative morality (evil actions)

- **🔄 Turn**: Current turn number

#### 3. Room Information Panel
```
┌─────────────────────── ⚑ CURRENT ROOM ⚑ ───────────────────────┐
│ Dark Corridor                                                    │
│                                                                  │
│ Coordinates: (2, 3)                                             │
│ Danger Level: ⚠⚠⚠ (3/5)                                        │
│ Searched: No                                                     │
│                                                                  │
│ A narrow passage with walls that seem to breathe...             │
└──────────────────────────────────────────────────────────────────┘
```

Features:
- **Room Name**: Color-coded by danger level
  - Green: Safe (0)
  - Cyan: Low danger (1)
  - Yellow: Moderate danger (2)
  - Orange: High danger (3)
  - Red: Extreme danger (4-5)

- **Grid Coordinates**: Shows player position on the map
- **Danger Level**: Visual representation with warning symbols
- **Search Status**: Indicates if the room has been searched
- **Room Description**: Atmospheric text describing the environment

#### 4. Objects in Room
When objects are present, displays a table:
```
┌─────────────────────────────────────────────────────────────────┐
│ OBJECT              │    TYPE     │    STATUS    │              │
├─────────────────────────────────────────────────────────────────┤
│ 📦 Old Chest        │   Chest     │   Locked     │              │
│ 🔥 Firepit          │  Campfire   │  Available   │              │
│ ⚠ Pressure Plate    │    Trap     │    Armed     │              │
└─────────────────────────────────────────────────────────────────┘
```

Object types and their icons:
- 📦 **Chest**: Locked or opened
- ⚠ **Trap**: Armed or disarmed
- 🔒 **Puzzle Gate**: Locked or open
- 🔥 **Campfire**: Available for rest
- ⚬ **Ground Item**: Can be picked up

#### 5. Available Exits
Shows all directions you can move:
```
Available Exits: ↑ North, → East, ↓ South
```

Direction indicators:
- ↑ North
- ↓ South
- → East
- ← West

#### 6. Atmospheric Text
Dynamic flavor text that changes based on your sanity level:
- **80-100 Sanity**: "The walls hold their shape. For now."
- **60-79 Sanity**: "Shadows move with deliberate purpose."
- **40-59 Sanity**: "Reality feels negotiable here."
- **20-39 Sanity**: "The geometry of this place disagrees with itself."
- **0-19 Sanity**: "You can taste the color of fear. It's copper and regret."

### HUD Features

#### Real-Time Updates
The HUD updates dynamically:
- When player stats change (health, sanity, morality)
- When entering a new room
- When objects appear or are interacted with
- When room state changes (searched, traps triggered)

#### Responsive Design
- Adapts to terminal width
- Scales bars and panels appropriately
- Maintains readability across different screen sizes

#### Color-Coded Information
All information is color-coded for quick visual assessment:
- **Green**: Positive, safe, healthy
- **Yellow**: Caution, moderate
- **Orange**: Warning, concerning
- **Red**: Danger, critical
- **Cyan**: Interactive, informational
- **Magenta**: Unusual, reality-bending

## API Reference

### GameHUD Class

#### Methods

```csharp
// Render the complete HUD with all information
GameHUD.RenderFullHUD(RunState run, RoomInstance room, List<WorldObjectInstance>? visibleObjects = null)

// Render just the player stats bar (quick update)
GameHUD.RenderPlayerStatsBar(RunState run)

// Render room details panel
GameHUD.RenderRoomDetailsPanel(RoomInstance room)

// Show controller hints
GameHUD.ShowControllerHints(bool controllerConnected)
```

### ControllerInput Class

#### Methods

```csharp
// Check if controller is connected
bool IsConnected

// Button checks (edge-triggered, only once per press)
bool IsAButtonPressed()
bool IsBButtonPressed()
bool IsXButtonPressed()
bool IsYButtonPressed()
bool IsDPadUpPressed()
bool IsDPadDownPressed()
bool IsDPadLeftPressed()
bool IsDPadRightPressed()

// Thumbstick position (-1.0 to 1.0)
(float x, float y) GetLeftThumbstick(float deadzone = 0.2f)

// Thumbstick movement checks (edge-triggered)
bool IsLeftThumbstickUp()
bool IsLeftThumbstickDown()

// Vibration feedback
void Vibrate(float leftMotor = 0.5f, float rightMotor = 0.5f, int durationMs = 200)
```

### ControllerMenu Class

```csharp
// Display an interactive menu with controller/keyboard support
int ShowMenu(string title, List<(string option, string description)> items, int startIndex = 0)
// Returns: Selected index, or -1 if cancelled
```

## Implementation Details

### Controller Input System
- Uses SharpDX.XInput for native XInput support
- Polling rate: 50ms for responsive input
- Edge detection: Prevents input repeat/bouncing
- Deadzone: 0.2 (20%) for thumbstick drift prevention

### HUD Rendering
- Built on Spectre.Console for rich terminal UI
- Panel-based layout with borders and styling
- Table components for structured data
- Markup system for colors and formatting

### Performance
- Efficient screen clearing and redrawing
- Minimal memory allocation
- Optimized string building
- Cached color calculations

## Troubleshooting

### Controller Not Detected
1. Ensure controller is connected before starting the game
2. Check if controller works in other applications
3. Try unplugging and reconnecting
4. Restart the game after connecting

### Input Lag
- Normal polling interval is 50ms
- Ensure no other applications are using the controller
- Check for USB connection issues

### Display Issues
- Ensure terminal supports ANSI colors
- Use Windows Terminal, ConEmu, or similar modern terminals
- Avoid legacy Command Prompt (cmd.exe)

## Future Enhancements
- Controller button remapping
- Adjustable vibration intensity
- Right thumbstick camera controls (if applicable)
- More complex haptic patterns
- Support for additional controller types
- Accessibility options (high contrast, large text)

