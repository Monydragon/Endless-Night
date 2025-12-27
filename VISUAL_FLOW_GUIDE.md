# Endless Night - Visual Flow Guide

## Main Menu
```
Endless Night
Goal: Find the House's heart and escape the Night.

[grey]Main Menu[/]
  • Continue
  • New Game
  • New Game (Seeded)
  • Inspect Saves (Debug)
  • Reset DB (Debug)
  • Recreate DB (Fix tables)
  • Quit
```

## New Game Intro Screen

When player starts a new game, they see an atmospheric intro:

```
═══════════════════════════════════════════════════════════════════════════
                        The Endless Night
═══════════════════════════════════════════════════════════════════════════

[cyan]A cryptic memory pulls at the edges of your mind. Find the House's heart. 
Something waits there.[/]

[grey]Gather artifacts that resonate with power. Solve the House's puzzles. 
Survive its surprises.[/]

[grey]Your sanity will fracture. Your morality will be tested. What you become 
matters as much as what you find.[/]

[bold magenta]The darkness beckons...[/]

[grey]Press Enter to descend.[/]
```

*Goal and context lines are seeded per run - same seed always produces same intro*

## Room Display During Gameplay

```
╭─────────────────────────────────────────────╮
│ [bold red]Nursery of Echoes[/]               │
│ [dim](3, 5)[/]  [bold orange3]⚠ Danger:[/] [bold red]3[/] │
╰─────────────────────────────────────────────╯

[magenta]You keep seeing doors that won't admit to being doors.[/]

[bold cyan]❤ Health:[/] [bold red]42[/]  [bold cyan]⚡ Sanity:[/] [bold magenta]18[/]  
[bold cyan]⚖ Morality:[/] [bold red]↓ -15[/]  [bold cyan]🔄 Turn:[/] [bold white]23[/]

[grey]What do you do?[/]
  • Move
  • Search Room
  • Interact
  • Inventory
  • Rest (Campfire)
  • Toggle Debug
  • Quit
```

### Color Legend in Room Display

| Element | Low (Green) | Med (Yellow) | High (Red) | Special |
|---------|-----------|------------|----------|---------|
| Room Name | Blue | Yellow | Red | Matches Danger |
| Health | Green | Yellow | Red | ❤ Icon |
| Sanity | Green | Cyan | Red | ⚡ Icon |
| Morality | Green ↑ | Grey → | Red ↓ | Shows Direction |
| Exposition | Happy | Tense | Chaotic | Colored by Sanity |

## Safe Room (Low Danger)
```
╭──────────────────────────────╮
│ [bold green]Library[/]           │
│ [dim](1, 2)[/]  [bold orange3]⚠ Danger:[/] [bold yellow]0[/] │
╰──────────────────────────────╯

[green]The walls behave. Mostly.[/]

[bold cyan]❤ Health:[/] [bold green]95[/]  [bold cyan]⚡ Sanity:[/] [bold green]87[/]  
[bold cyan]⚖ Morality:[/] [bold green]↑ 5[/]  [bold cyan]🔄 Turn:[/] [bold white]3[/]
```

## Dangerous Room (High Danger)
```
╭──────────────────────────────╮
│ [bold red]Nursery[/]            │
│ [dim](2, 8)[/]  [bold orange3]⚠ Danger:[/] [bold red]3[/] │
╰──────────────────────────────╯

[bold red]Reality is threadbare here. Your breath draws patterns that 
don't hold.[/]

[bold cyan]❤ Health:[/] [bold red]12[/]  [bold cyan]⚡ Sanity:[/] [bold red]5[/]  
[bold cyan]⚖ Morality:[/] [bold red]↓ -45[/]  [bold cyan]🔄 Turn:[/] [bold white]78[/]
```

## Inventory Screen
```
╭──────────────────╮
│ Item       │ Qty  │
├──────────────────┤
│ bandage    │ 2    │
│ rusty-key  │ 1    │
│ torch      │ 1    │
│ health-pot │ 3    │
╰──────────────────╯
```

## Interaction Menu
```
[grey]Interact with what?[/]
  • Pick up: torch
  • Chest
  • Rest at Firepit
  • Back
```

## Explore Actions
```
[grey]What do you do?[/]
  • Move
  • Search Room
  • Interact
  • Inventory
  • Rest (Campfire)
  • Toggle Debug
  • Quit
```

## Key Visual States

### Healthy State
- Bright green health
- Steady cyan sanity
- Green upward morality arrow
- Green atmospheric text
- Danger level: 0-1 (blue/green)

### Stressed State
- Yellow/orange health
- Magenta sanity (fractured)
- Grey neutral morality
- Yellow/cyan atmospheric text
- Danger level: 2 (yellow)

### Critical State
- Red health (dangerously low)
- Red sanity (unraveling)
- Red downward morality arrow
- Bold red atmospheric text
- Danger level: 3+ (red)

## Theme Elements

✓ **Dark Undertones**: Dark gray borders, magenta mysteries, red dangers
✓ **Colorful & Readable**: Each stat has distinct color range
✓ **Surprise Highlights**: Sudden red warnings, cyan mysteries
✓ **Emoji Support**: Heart ❤, Lightning ⚡, Scale ⚖, Cycle 🔄, Warning ⚠
✓ **Seeded Consistency**: Same seed = same intro & RNG outputs
✓ **Atmospheric Clarity**: Player always knows current threat level

## Dynamic Elements

- **Intro dialogue**: Changes based on seed (6 variations × 4 contexts = 24+ unique combinations)
- **Exposition line**: Changes based on sanity (5 different messages)
- **Color coding**: Updates every turn based on stats
- **Room danger**: Visually distinct by color gradient
- **Morality direction**: Shows ↑↓→ based on current value

---

This creates an immersive, dark-themed experience where the player's condition is always visible at a glance, and each new game feels fresh through seeded intro dialogue.

