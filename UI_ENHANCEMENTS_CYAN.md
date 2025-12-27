# Endless Night - Enhanced UI with Cyan Theme & Dynamic Options

## Update Summary

### Major Changes
1. **All grey text replaced with cyan** for consistent, colorful dialogue and interactions
2. **Dynamic action menu** - Only relevant options appear based on room state
3. **Controller-friendly navigation** - Structured menus with arrow key support via Spectre.Console
4. **Uniform UI layout** - All menus now use consistent borders, headers, and descriptions
5. **Real-time screen updates** - UI clears and refreshes for clean presentation

## What Changed

### Text Color Scheme
- ❌ **Removed**: All grey (`[grey]...[/]`) text
- ✅ **Added**: Cyan (`[cyan]...[/]`) for all dialogue, prompts, and interaction text
- **Result**: Cleaner, more colorful interface that fits the dark theme

### Dynamic Action Menu
The main action menu now intelligently shows only available options:

```
═══════════════════════════════════════════
AVAILABLE ACTIONS
═══════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Search Room - Search for hidden items and triggers
▸ Interact - Use objects in the room
▸ Inventory - View your collected items
▸ Rest (Campfire) - Restore health and sanity
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═══════════════════════════════════════════

➤ Choose action (Use ↑↓ arrows, press Enter):
```

**Logic**:
- **Move** - Always available (if exits exist)
- **Search Room** - Only if hidden objects exist and room not yet searched
- **Interact** - Only if visible interactive objects exist
- **Inventory** - Always available
- **Rest (Campfire)** - Only if campfire is in current room
- **Toggle Debug** - Always available
- **Quit** - Always available

### Interact Menu
```
═══════════════════════════════════════════
INTERACT WITH
═══════════════════════════════════════════
▸ Pick up: torch
  A burning stick that casts dancing shadows.
▸ 📦 Chest (locked)
  A warped chest with a silver lock.
▸ 🔥 Firepit (rest here)
  A small firepit crackles here. Warmth feels almost like a memory.
▸ 🔙 Back
═══════════════════════════════════════════

➤ Choose (↑↓ arrows, Enter):
```

**Features**:
- Item descriptions shown directly
- Emoji icons for quick identification:
  - 📦 = Chest
  - ⚠ = Trap
  - 🔒/🔓 = Puzzle gate
  - 🔥 = Firepit
  - 🔙 = Back button
- State indicators (locked/open, armed/disarmed)

### Movement Menu
```
═══════════════════════════════════════════
CHOOSE DIRECTION
═══════════════════════════════════════════
▸ North
▸ East
▸ South
▸ 🔙 Back
═══════════════════════════════════════════

➤ Choose (↑↓ arrows, Enter):
```

**Features**:
- Only shows available directions
- Sorted alphabetically
- Back button with emoji
- Clear title separators

### Inventory Screen
```
═══════════════════════════════════════════
INVENTORY
═══════════════════════════════════════════
┌─────────────────┬─────┐
│ Item            │ Qty │
├─────────────────┼─────┤
│ torch           │  1  │
│ rusty-key       │  1  │
│ bandage         │  3  │
└─────────────────┴─────┘
═══════════════════════════════════════════
[cyan]Press Enter to continue...[/]
```

**Features**:
- Rounded table borders
- Cyan column headers
- Returns to main game after viewing

### Save Inspection
```
╭──────────────────────────────────────────╮
│           ▌ Save Inspect ▌                │
├──────────────────────────────────────────┤
│ [cyan]RunId[/]: {id}                      │
│ [cyan]Seed[/]: {seed}                     │
│ [cyan]Turn[/]: {turn}                     │
│ [cyan]Sanity[/]: {sanity}                 │
│ [cyan]Health[/]: {health}                 │
│ [cyan]Morality[/]: {morality}             │
│ [cyan]Room[/]: {room_name}                │
│ [cyan]Items[/]: {count}                   │
╰──────────────────────────────────────────╯
```

## UI Navigation Flow

### Controller/Keyboard Navigation
The game now uses **Spectre.Console's SelectionPrompt**, which supports:

```
↑ = Previous option
↓ = Next option
Enter = Select current option
```

This works on Windows, Linux, and macOS via console/terminal keyboard input.

### Example Flow
```
1. Start game
2. Main menu appears (↑↓ navigate, Enter to select)
3. Choose "New Game"
4. Intro dialogue displays (Press Enter)
5. Game loop starts
   - Room banner displays
   - Available actions shown
   - ↑↓ arrow keys navigate menu
   - Enter selects action
6. Action executes
7. Screen clears and refreshes
8. Back to step 5
```

## Visual Consistency

All menus now follow this pattern:

```
[bold cyan]═══════════════════════════════════════════[/]
[bold cyan]MENU TITLE[/]
[bold cyan]═══════════════════════════════════════════[/]
[cyan]▸ Option 1[/] - [dim]Description[/]
[cyan]▸ Option 2[/] - [dim]Description[/]
[cyan]▸ Option 3[/] - [dim]Description[/]
[bold cyan]═══════════════════════════════════════════[/]

[bold cyan]➤ Prompt text:[/]
```

### Colors Used
- **cyan** `[cyan]...[/]` - All interactive text and prompts
- **bold cyan** `[bold cyan]...[/]` - Headers and separators
- **dim** `[dim]...[/]` - Descriptions and helper text
- **green** `[green]✓ ...[/]` - Success messages
- **red** `[red]✗ ...[/]` - Error/warning messages
- **yellow/magenta/etc** - Stat-based colors (unchanged)

## Technical Implementation

### Key Methods
1. **GetAvailableActionsAsync()** - Determines which actions to display
2. **RenderActionMenu()** - Displays action options with descriptions
3. **ShowInventoryAsync()** - Displays inventory in table format
4. **InteractMenuAsync()** - Shows interactable objects with state
5. **MoveMenuAsync()** - Shows available directions
6. **GameLoopAsync()** - Main loop with screen clearing

### Screen Clearing
The game now clears the screen before each major UI update:
```csharp
AnsiConsole.Clear();  // Fresh screen before showing menu
```

This provides clean, professional presentation without UI clutter.

## Testing

✅ **All 10 NUnit tests passing**
- Procedural generation
- Run service operations
- Chest/puzzle mechanics
- Solvability validation

## User Experience Improvements

| Before | After |
|--------|-------|
| Grey, plain menus | Cyan, colorful, styled menus |
| All options always shown | Dynamic options based on room state |
| No descriptions | Item descriptions inline |
| Plain text prompts | Emoji-enhanced, structured menus |
| Screen clutter | Clean, cleared screens |
| Manual tracking of options | UI shows what's available |

## How to Test

```powershell
# Build
cd "C:\Projects\Github\Console\Endless-Night"
dotnet build

# Run game
dotnet run --project EndlessNight

# Run tests
cd Tests\EndlessNight.Tests
dotnet test
```

## Navigation Example

When you start a new game:

1. **Main Menu appears**
   - Use ↑↓ to highlight "New Game"
   - Press Enter

2. **Intro dialogue**
   - Press Enter to descend

3. **Room displays**
   - ❤ Health: 100 | ⚡ Sanity: 100 | ⚖ Morality: ↑ 0 | 🔄 Turn: 0
   - Action menu shows available options
   - Use ↑↓ to select action
   - Press Enter to execute

4. **Move example**
   - Select "Move"
   - Menu shows: North, South, Back
   - Use ↑↓ to choose direction
   - Press Enter to move

5. **Interact example**
   - Select "Interact"
   - Menu shows available objects with descriptions
   - Use ↑↓ to select object
   - Press Enter to interact

## Color Reference

### Dialogue & UI Text
- `[cyan]` = Interactive prompts and descriptions
- `[bold cyan]` = Section headers and separators
- `[dim]` = Helper text and descriptions

### Status Indicators
- `[green]✓` = Success
- `[red]✗` = Error/Warning
- `[yellow]` = Info message

### Game Stats (unchanged)
- Health colors: green → yellow → orange3 → red
- Sanity colors: green → cyan → magenta → red
- Morality: green ↑ / grey → / red ↓

---

**Status**: ✅ Complete - Cyan theme applied, dynamic menus working, controller support integrated, uniform UI visible throughout

