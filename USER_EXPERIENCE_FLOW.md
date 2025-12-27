# Endless Night - User Experience Flow

## Complete Gameplay Flow with Cyan Theme

### 1. Starting the Game

```
Endless Night
Goal: Find the House's heart and escape the Night.
Enter your [green]player name[/]: Mony

[bold cyan]Main Menu[/]
➤ Choose action (Use ↑↓ arrows, press Enter):
  ▸ Continue
  ▸ New Game          ← You select this
  ▸ New Game (Seeded)
  ▸ Inspect Saves (Debug)
  ▸ Reset DB (Debug)
  ▸ Recreate DB (Fix tables)
  ▸ Quit
```

### 2. Game Intro Screen

```
═════════════════════════════════════════════════════════════════════════════
                            The Endless Night
═════════════════════════════════════════════════════════════════════════════

[cyan]A cryptic memory pulls at the edges of your mind. Find the House's heart. 
Something waits there.[/]

[cyan]Gather artifacts that resonate with power. Solve the House's puzzles. 
Survive its surprises.[/]

[cyan]Your sanity will fracture. Your morality will be tested. What you become 
matters as much as what you find.[/]

[bold magenta]The darkness beckons...[/]

[cyan]Press Enter to descend.[/]
```
*User presses Enter*

### 3. Game Starts - Room Display

```
╭─────────────────────────────────────────╮
│ [bold green]Foyer[/]                      │
│ [dim](0, 0)[/]  [bold orange3]⚠ Danger:[/] [bold yellow]0[/]  │
╰─────────────────────────────────────────╯

[green]The walls behave. Mostly.[/]

[bold cyan]❤ Health:[/] [bold green]100[/]  [bold cyan]⚡ Sanity:[/] [bold green]100[/]  
[bold cyan]⚖ Morality:[/] [bold grey]→ 0[/]  [bold cyan]🔄 Turn:[/] [bold white]0[/]

═══════════════════════════════════════════
AVAILABLE ACTIONS
═══════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Search Room - Search for hidden items and triggers
▸ Interact - Use objects in the room
▸ Inventory - View your collected items
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═══════════════════════════════════════════

➤ Choose action (Use ↑↓ arrows, press Enter):
```

### 4. Player Chooses "Move"

```
═════════════════════════════════════════════
CHOOSE DIRECTION
═════════════════════════════════════════════
▸ North         ← You select this (↓ then Enter)
▸ 🔙 Back
═════════════════════════════════════════════

➤ Choose (↑↓ arrows, Enter):
```

### 5. After Moving - New Room

```
╭─────────────────────────────────────────╮
│ [bold blue]Hallway[/]                     │
│ [dim](0, 1)[/]  [bold orange3]⚠ Danger:[/] [bold yellow]1[/]  │
╰─────────────────────────────────────────╯

[yellow]Something watches with polite hunger.[/]

[bold cyan]❤ Health:[/] [bold green]100[/]  [bold cyan]⚡ Sanity:[/] [bold green]100[/]  
[bold cyan]⚖ Morality:[/] [bold grey]→ 0[/]  [bold cyan]🔄 Turn:[/] [bold white]1[/]

═══════════════════════════════════════════
AVAILABLE ACTIONS
═══════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Search Room - Search for hidden items and triggers
▸ Interact - Use objects in the room
▸ Inventory - View your collected items
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═══════════════════════════════════════════
```

### 6. Player Chooses "Interact"

```
═════════════════════════════════════════════
INTERACT WITH
═════════════════════════════════════════════
▸ Pick up: torch
  A burning stick that casts dancing shadows.
▸ 📦 Chest (locked)
  A warped chest with a corroded lock.
▸ 🔙 Back
═════════════════════════════════════════════

➤ Choose (↑↓ arrows, Enter):
```
*Player selects torch*

```
[green]✓ You pick up torch.[/]
```
*Screen pauses for 2 seconds, then returns to room display*

### 7. Player Chooses "Inventory"

```
═════════════════════════════════════════════
INVENTORY
═════════════════════════════════════════════
┌──────────────────┬─────┐
│ [cyan]Item[/]        │ [cyan]Qty[/] │
├──────────────────┼─────┤
│ torch            │  1  │
│ lantern          │  1  │
└──────────────────┴─────┘
═════════════════════════════════════════════
[cyan]Press Enter to continue...[/]
```

### 8. Deep in the House - Dangerous Room

```
╭──────────────────────────────────────────╮
│ [bold red]Nursery of Echoes[/]            │
│ [dim](2, 5)[/]  [bold orange3]⚠ Danger:[/] [bold red]3[/]    │
╰──────────────────────────────────────────╯

[bold red]Reality is threadbare here. Your breath draws patterns that 
don't hold.[/]

[bold cyan]❤ Health:[/] [bold orange3]35[/]  [bold cyan]⚡ Sanity:[/] [bold magenta]18[/]  
[bold cyan]⚖ Morality:[/] [bold red]↓ -25[/]  [bold cyan]🔄 Turn:[/] [bold white]23[/]

═════════════════════════════════════════════
AVAILABLE ACTIONS
═════════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Interact - Use objects in the room
▸ Inventory - View your collected items
▸ Rest (Campfire) - Restore health and sanity
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═════════════════════════════════════════════
```

### 9. Player Finds Campfire - Rest

```
[green]✓ Warmth steadies your breathing. The Night steps back.[/]
```

*After rest, room display updates:*

```
[bold cyan]❤ Health:[/] [bold yellow]52[/]  [bold cyan]⚡ Sanity:[/] [bold cyan]40[/]  
[bold cyan]⚖ Morality:[/] [bold red]↓ -25[/]  [bold cyan]🔄 Turn:[/] [bold white]24[/]
```

### 10. Player Searches Room

```
═════════════════════════════════════════════
ACTION: SEARCH ROOM
═════════════════════════════════════════════

[green]✓ You search and find: sigil, bandage, health-potion.[/]
```

*Screen pauses, items added to inventory*

### 11. Critical State - Many Options Removed

```
╭──────────────────────────────────────────╮
│ [bold red]The Listening Chamber[/]       │
│ [dim](1, 8)[/]  [bold orange3]⚠ Danger:[/] [bold red]3[/]      │
╰──────────────────────────────────────────╯

[bold red]Every shadow knows where you sleep.[/]

[bold cyan]❤ Health:[/] [bold red]5[/]  [bold cyan]⚡ Sanity:[/] [bold red]2[/]  
[bold cyan]⚖ Morality:[/] [bold red]↓ -80[/]  [bold cyan]🔄 Turn:[/] [bold white]52[/]

═════════════════════════════════════════════
AVAILABLE ACTIONS
═════════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Interact - Use objects in the room
▸ Inventory - View your collected items
▸ Rest (Campfire) - Restore health and sanity
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═════════════════════════════════════════════
```

*Note: "Search Room" removed - already searched*

### 12. Debug Mode Active

```
═════════════════════════════════════════════
AVAILABLE ACTIONS
═════════════════════════════════════════════
▸ Move - Navigate to an adjacent room
▸ Search Room - Search for hidden items and triggers
▸ Interact - Use objects in the room (5 objects)
▸ Inventory - View your collected items (7 items)
▸ Rest (Campfire) - Restore health and sanity
▸ Toggle Debug - Enable/disable debug information
▸ Quit - Exit the game
═════════════════════════════════════════════
```

*Debug mode shows object/item counts*

## Color Changes During Gameplay

### Health Status Transitions
```
Start:    [bold cyan]❤ Health:[/] [bold green]100[/]  ← Excellent
Damage:   [bold cyan]❤ Health:[/] [bold yellow]60[/]   ← Warning
Trap:     [bold cyan]❤ Health:[/] [bold orange3]25[/]  ← Critical
Dying:    [bold cyan]❤ Health:[/] [bold red]5[/]     ← Dire
```

### Sanity Transitions
```
Calm:      [bold cyan]⚡ Sanity:[/] [bold green]85[/]   ← Peaceful
Unsettled: [bold cyan]⚡ Sanity:[/] [bold cyan]50[/]    ← Tense
Fracturing:[bold cyan]⚡ Sanity:[/] [bold magenta]25[/] ← Broken
Unraveling:[bold cyan]⚡ Sanity:[/] [bold red]5[/]      ← Lost
```

### Morality Shifts
```
Good:     [bold cyan]⚖ Morality:[/] [bold green]↑ 45[/]  ← Virtuous
Neutral:  [bold cyan]⚖ Morality:[/] [bold grey]→ 0[/]    ← Balanced
Evil:     [bold cyan]⚖ Morality:[/] [bold red]↓ -45[/]   ← Corrupted
```

## UI Elements Always Visible

✅ **Room Banner**
- Name (color-coded by danger)
- Coordinates (X, Y)
- Danger rating (⚠)

✅ **Stat Bar**
- Health (❤) with live color
- Sanity (⚡) with live color
- Morality (⚖) with direction symbol
- Turn counter (🔄)

✅ **Atmospheric Text**
- Changes based on sanity level
- Provides mood/description

✅ **Action Menu**
- Cyan-colored options
- Only relevant actions shown
- Descriptions for each action

✅ **Keyboard Navigation**
- ↑ Previous option
- ↓ Next option
- Enter to select

## Navigation Tips

1. **Main Menu** - Use ↑↓ and Enter
2. **In Game** - Each turn, choose an action
3. **Submenus** - Always show back button (🔙)
4. **Screen Clears** - Between major actions
5. **Pauses** - After actions complete
6. **Consistent** - Same layout every time

## Theme

- **Dark Gothic Atmosphere** - Red dangers, cyan help
- **Colorful but Thematic** - Not garish, fits the mood
- **Clear Communication** - Always know what you can do
- **Immersive** - Text matches the horror tone
- **Responsive** - UI updates to your situation

---

**This is the complete user experience - immersive, clear, and in full cyan theme!**

